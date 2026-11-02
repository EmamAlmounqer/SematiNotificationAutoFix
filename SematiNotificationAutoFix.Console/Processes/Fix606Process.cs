using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NobillCalls;
using SematiNotificationAutoFix.Console.Models;
using SematiNotificationAutoFix.Console.Services;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using Serilog.Context;
using System.Text.Json;

namespace SematiNotificationAutoFix.Console.Processes;

public class Fix606Process
{
    private const int CallLogCandidateWindow = 10;

    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<Fix606Process> _logger;
    private readonly TerminationService _terminationProcess;
    private readonly int _sematiServiceCallLogCutOffId;
    private readonly int _sematiNotificaitonCutOff;
    private readonly NobillServiceClient _nobill;

    public Fix606Process(IConfiguration configuration, ActivationDbContext dbContext, ILogger<Fix606Process> logger, TerminationService terminationProcess, NobillServiceClient nobillClient)
    {
        _dbContext = dbContext;
        _logger = logger;
        _terminationProcess = terminationProcess;
        _nobill = nobillClient;
        _sematiServiceCallLogCutOffId = configuration.GetValue<int>("ProcessOptions:SematiServiceCallLogCutOffId");
        _sematiNotificaitonCutOff = configuration.GetValue<int>("ProcessOptions:SematiNotificaitonCutOff");
    }

    public async Task<List<int>> ProcessAsync(List<int> actionIds)
    {
        var successfulIds = new List<int>();
        foreach (var id in actionIds)
        {
            try
            {
                if (await ProcessAsync(id))
                    successfulIds.Add(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception processing action {ActionId}", id);
            }
        }
        return successfulIds;
    }

    public async Task<List<int>> ProcessAsync(List<SematiNotificationAction> sematiNotificationActionId)
    {
        var successfulIds = new List<int>();
        foreach (var action in sematiNotificationActionId)
        {
            try
            {
                if (await ProcessAsync(action))
                    successfulIds.Add(action.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception processing action {ActionId}", action.Id);
            }
        }
        return successfulIds;
    }

    public async Task<bool> ProcessAsync(int sematiNotificationActionId)
    {
        using var _ = LogContext.PushProperty("ProcessName", "Fix606");
        using var __ = LogContext.PushProperty("ActionId", sematiNotificationActionId);

        _logger.LogInformation("Processing action {ActionId}", sematiNotificationActionId);

        var action = await _dbContext.SematiNotificationActions
                                                               .Include(x => x.SematiNotification)
                                                               .AsNoTracking()
                                                               .FirstOrDefaultAsync(x => x.Id == sematiNotificationActionId);

        if (action?.SematiUpdateTcn is null)
        {
            _logger.LogWarning("Action {ActionId} not found or has no TCN — skipping", sematiNotificationActionId);
            return false;
        }

        return await ProcessAsync(action);
    }

    public async Task<bool> ProcessAsync(SematiNotificationAction action)
    {
        using var _ = LogContext.PushProperty("ProcessName", "Fix606");
        using var __ = LogContext.PushProperty("ActionId", action.Id);
        using var ___ = LogContext.PushProperty("NotificationId", action.SematiNotificationId);

        if (action.SematiUpdateCode == "600" || action.SematiUpdateCode == "780")
        {
            _logger.LogWarning("Action {ActionId} has SematiUpdateCode {SematiUpdateCode}  — skipping", action.Id, action.SematiUpdateCode);
            return false;
        }

        var personId = action.SematiNotification.IdNumber;
        if (personId is null)
        {
            _logger.LogWarning("No PersonId found for action {ActionId} — skipping", action.Id);
            return false;
        }

        if (action.SematiNotification.NotificationCode >= 8)
        {
            _logger.LogWarning("Not Valid Notification Code {NotificationCode} for notfication action {ActionId} — skipping", action.SematiNotification.NotificationCode, action.Id);
            return false;
        }

        using var ____ = LogContext.PushProperty("PersonId", personId);


        var candidates = await _dbContext.SematiServiceCallLogs.AsNoTracking()
                                                               .Where(x => x.Id > _sematiServiceCallLogCutOffId
                                                                           && x.TCN == action.SematiUpdateTcn
                                                                           && x.Code == 606
                                                                           && x.Operation == "UpdateSematiNotification")
                                                               .OrderByDescending(x => x.Id)
                                                               .Select(x => new { x.Id, x.RequestText, x.ResponseText })
                                                               .Take(CallLogCandidateWindow)
                                                               .ToListAsync();

        var callLog = candidates.FirstOrDefault(x => x.RequestText.Contains(personId));

        if (callLog is null)
        {
            _logger.LogWarning("No 606 service call log found for action {ActionId} (TCN={Tcn}) — skipping", action.Id, action.SematiUpdateTcn);
            return false;
        }

        var pendingNumbers = JsonSerializer.Deserialize<SematiServiceResponse>(callLog.ResponseText)?.PendingNumbers;
        if (pendingNumbers is null)
        {
            _logger.LogWarning("Could not deserialize pending numbers for action {ActionId} — skipping", action.Id);
            return false;
        }

        _logger.LogInformation("Found {Count} pending numbers for action {ActionId}: {@Numbers}", pendingNumbers.Count, action.Id, pendingNumbers);

        var allPendingNumberTerminateSucceeded = true;
        foreach (var number in pendingNumbers)
        {
            using var _____ = LogContext.PushProperty("MSISDN", number);

            var customerData = await _nobill.GetCustomerDataAsync(number);

            if (customerData is null || new List<int> { 0, 3 }.Contains(customerData.Body.GetCustomerDataResult))
            {
                _logger.LogError("Cann't get data from nobill");
                return false;
            }

            var accountNumber = customerData?.Body?.details?.CustomerNum;
            var customerId = customerData?.Body?.details?.CustomerID;

            if (customerId == personId)
            {
                _logger.LogWarning("Pending number {Number} still active in nobill for person id {PersonId}", action.Id, personId);

                var sematiNotificationActionStepId = GetSematiNotificationActionStepIdByCode(action.SematiNotification.NotificationCode);
                if (sematiNotificationActionStepId == -1)
                {
                    _logger.LogWarning("No SematiNotificationActionStep mapping found for notification code {NotificationCode} for notification {NotificationId}", action.SematiNotification.NotificationCode, action.SematiNotification.Id);
                    return false;
                }

                var notification = await _dbContext.SematiNotificationActions.Where(a => a.MSISDN == number && a.SematiNotification.IdNumber == personId).FirstOrDefaultAsync();


                _logger.LogInformation("INSERT INTO Activation.dbo.SematiNotificationAction (MSISDN,ExpectedActionDate,CreatedAt,Status,SematiNotificationId,SematiNotificationActionStepId,AccountNumber,IsTcnUpdated) VALUES ('{number}',GETDATE(),GETDATE(),2,{SematiNotificationId},{SematiNotificationActionStepId},'{AccountNumber}',0)", number, action.SematiNotificationId, sematiNotificationActionStepId, accountNumber);

                //var newAction = new SematiNotificationAction
                //{
                //    MSISDN = number,
                //    ExpectedActionDate = DateTime.Now,
                //    CreatedAt = DateTime.Now,
                //    Status = (int)SematiNotificationStatus.Created,
                //    SematiNotificationId = action.SematiNotificationId,
                //    SematiNotificationActionStepId = sematiNotificationActionStepId,
                //    AccountNumber = accountNumber
                //};

                //_dbContext.SematiNotificationActions.Add(newAction);
                //await _dbContext.SaveChangesAsync();
                //_logger.LogInformation("Created new SematiNotificationAction for notification {NotificationId} with MSISDN {MSISDN} and AccountNumber {AccountNumber}", newAction.Id, newAction.MSISDN, newAction.AccountNumber);
                return false;
            }

            var terminationResult = await _terminationProcess.TerminateAndSaveAsync(number, personId);

            if (!terminationResult.IsTerminationSuccess())
                allPendingNumberTerminateSucceeded = false;
        }
        return allPendingNumberTerminateSucceeded;
    }

    public async Task<List<int>> Get606FromDB()
    {
        using var _ = LogContext.PushProperty("ProcessName", "Fix606");

        var notifications = await _dbContext.SematiNotificationActions.AsNoTracking()
            .Include(x => x.SematiNotification)
            .Where(a => a.SematiNotification.Id > _sematiNotificaitonCutOff
                     && a.SematiNotification.NotificationCode < 8
                     && a.SematiNotificationActionStepId % 2 == 0
                     && a.ExecutedAt != null
                     && a.SematiUpdateCode == "606"
                     && !_dbContext.SematiNotificationActions.Any(x =>
                            x.SematiNotificationId == a.SematiNotificationId
                         && x.SematiNotification.Id > _sematiNotificaitonCutOff
                         && x.SematiNotificationActionStepId % 2 == 0
                         && x.ExecutedAt != null
                         && (x.SematiUpdateCode == "600" || x.SematiUpdateCode == "780")))
            .Select(a => a.Id)
            .Distinct()
            .ToListAsync();

        return notifications;
    }

    private int GetSematiNotificationActionStepIdByCode(int code)
    {
        return code switch
        {
            1 => 2,
            3 => 4,
            4 => 6,
            5 => 8,
            11 => 12,
            _ => -1,
        };
    }
}
