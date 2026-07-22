using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Models;
using SematiNotificationAutoFix.Console.Services;
using SematiNotificationAutoFix.DAL.Data;
using Serilog.Context;
using System.Text.Json;

namespace SematiNotificationAutoFix.Console.Processes;

public class Fix606Process
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<Fix606Process> _logger;
    private readonly TerminationService _terminationProcess;
    private readonly int _sematiServiceCallLogCutOffId;

    public Fix606Process(IConfiguration configuration, ActivationDbContext dbContext, ILogger<Fix606Process> logger, TerminationService terminationProcess)
    {
        _dbContext = dbContext;
        _logger = logger;
        _terminationProcess = terminationProcess;
        _sematiServiceCallLogCutOffId = configuration.GetValue<int>("ProcessOptions:SematiServiceCallLogCutOffId");
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

    public async Task<bool> ProcessAsync(int sematiNotificationActionId)
    {
        using var _ = LogContext.PushProperty("ProcessName", "Fix606");
        using var __ = LogContext.PushProperty("ActionId", sematiNotificationActionId);

        _logger.LogInformation("Processing action {ActionId}", sematiNotificationActionId);

        var action = await _dbContext.SematiNotificationActions.AsNoTracking()
                                                               .Include(x => x.SematiNotification)
                                                               .AsNoTracking()
                                                               .FirstOrDefaultAsync(x => x.Id == sematiNotificationActionId);

        if (action?.SematiUpdateTcn is null)
        {
            _logger.LogWarning("Action {ActionId} not found or has no TCN — skipping", sematiNotificationActionId);
            return false;
        }

        using var ___ = LogContext.PushProperty("NotificationId", action.SematiNotificationId);

        if (action.SematiUpdateCode == "600" || action.SematiUpdateCode == "780")
        {
            _logger.LogWarning("Action {ActionId} has SematiUpdateCode {SematiUpdateCode}  — skipping", sematiNotificationActionId, action.SematiUpdateCode);
            return false;
        }

        var personId = action.SematiNotification.IdNumber;
        if (personId is null)
        {
            _logger.LogWarning("No PersonId found for action {ActionId} — skipping", sematiNotificationActionId);
            return false;
        }

        using var ____ = LogContext.PushProperty("PersonId", personId);

        var callLog = await _dbContext.SematiServiceCallLogs.AsNoTracking()
                                                            .Where(x => x.Id > _sematiServiceCallLogCutOffId
                                                                        && x.TCN == action.SematiUpdateTcn
                                                                        && x.Operation == "UpdateSematiNotification"
                                                                        && x.RequestText.Contains(action.SematiNotification.IdNumber)
                                                                        && x.Code == 606)
                                                            .OrderByDescending(x => x.Id)
                                                            .FirstOrDefaultAsync();

        if (callLog is null || callLog.Code != 606)
        {
            _logger.LogWarning("No 606 service call log found for action {ActionId} (TCN={Tcn}) — skipping", sematiNotificationActionId, action.SematiUpdateTcn);
            return false;
        }

        var pendingNumbers = JsonSerializer.Deserialize<SematiServiceResponse>(callLog.ResponseText)?.PendingNumbers;
        if (pendingNumbers is null)
        {
            _logger.LogWarning("Could not deserialize pending numbers for action {ActionId} — skipping", sematiNotificationActionId);
            return false;
        }

        _logger.LogInformation("Found {Count} pending numbers for action {ActionId}: {@Numbers}", pendingNumbers.Count, sematiNotificationActionId, pendingNumbers);

        var allPendingNumberTerminateSucceeded = true;
        foreach (var number in pendingNumbers)
        {
            using var _____ = LogContext.PushProperty("MSISDN", number);
            var terminationResult = await _terminationProcess.TerminateAndSaveAsync(number, personId);

            if (!terminationResult.IsTerminationSuccess())
                allPendingNumberTerminateSucceeded = false;
        }
        return allPendingNumberTerminateSucceeded;
    }
}
