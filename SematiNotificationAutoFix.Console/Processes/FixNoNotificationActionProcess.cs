using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Enums;
using SematiNotificationAutoFix.Console.Models;
using SematiNotificationAutoFix.Console.Services;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using Serilog.Context;

namespace SematiNotificationAutoFix.Console.Processes;

public class FixNoNotificationActionProcess
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<FixNoNotificationActionProcess> _logger;
    private readonly TerminationService _terminationProcess;
    private readonly NumberService _numberService;

    public FixNoNotificationActionProcess(ActivationDbContext dbContext, ILogger<FixNoNotificationActionProcess> logger, TerminationService terminationProcess, NumberService numberService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _terminationProcess = terminationProcess;
        _numberService = numberService;
    }

    public async Task<List<int>> TerminateAsync(List<int> sematiNotificationIds)
    {
        LogContext.PushProperty("ProcessName", "FixNoNotificationAction");
        var createdActionIds = new List<int>();

        var notifications = await _dbContext.SematiNotifications.AsNoTracking()
                                                                .Where(x => sematiNotificationIds.Contains(x.Id))
                                                                .ToListAsync();

        foreach (var notification in notifications)
        {
            try
            {
                var actionIds = await TerminateAsync(notification);
                createdActionIds.AddRange(actionIds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception terminating Semati Notification {SematiNotificationId}", notification.Id);
            }
        }

        return createdActionIds;
    }

    private async Task<List<int>> TerminateAsync(SematiNotification notification)
    {
        LogContext.PushProperty("NotificationId", notification.Id);

        if (_dbContext.SematiNotificationActions.Any(x => x.SematiNotificationId == notification.Id))
        {
            _logger.LogWarning("Notification {NotificationId} already has an action associated with it.", notification.Id);
            return [];
        }

        var sematiNotificationActionStepId = GetSematiNotificationActionStepIdByCode(notification.NotificationCode);
        if (sematiNotificationActionStepId == -1)
        {
            _logger.LogWarning("No SematiNotificationActionStep mapping found for notification code {NotificationCode} for notification {NotificationId}", notification.NotificationCode, notification.Id);
            return [];
        }

        using var ___ = LogContext.PushProperty("PersonId", notification.IdNumber);

        var msisdnsDatas = await _numberService.FetchMsisdnsAndAccountNumber(notification);

        List<SematiNotificationAction> newActions = [];
        foreach (var msisdnData in msisdnsDatas)
        {
            using var ____ = LogContext.PushProperty("MSISDN", msisdnData.Msisdn);
            var terminationResult = await _terminationProcess.TerminateAndSaveAsync(msisdnData.Msisdn, notification.IdNumber);

            if (!terminationResult.IsTerminationSuccess())
            {
                _logger.LogWarning("Termination Form {MSISDN} has ResponseCode {ResponseCode} — skipping", msisdnData.Msisdn, terminationResult.ResponseCode);
                continue;
            }

            var newAction = new SematiNotificationAction
            {
                MSISDN = msisdnData.Msisdn,
                ExpectedActionDate = DateTime.Now,
                CreatedAt = DateTime.Now,
                Status = (int)SematiNotificationStatus.HasErrors,
                SematiNotificationId = notification.Id,
                SematiNotificationActionStepId = sematiNotificationActionStepId,
                AccountNumber = msisdnData.AccountNumber
            };

            newActions.Add(newAction);
            _logger.LogInformation("Created new SematiNotificationAction for notification {NotificationId} with MSISDN {MSISDN} and AccountNumber {AccountNumber}", notification.Id, msisdnData.Msisdn, msisdnData.AccountNumber);
        }

        _dbContext.SematiNotificationActions.AddRange(newActions);
        await _dbContext.SaveChangesAsync();

        var newActionIds = newActions.Select(a => a.Id).ToList();
        _logger.LogInformation("Saved {Count} new actions for notification {NotificationId} — IDs: {@Ids}, MSISDNs: {@MSISDNs}, AccountNumber: {@AccountNumber}",
            newActions.Count,
            notification.Id,
            newActionIds,
            newActions.Select(a => a.MSISDN),
            newActions.Select(a => a.AccountNumber));

        return newActionIds;
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

    public static bool IsBorderNumber(string? idNumber)
    {
        if (idNumber == null) { return false; }

        bool isTenDigitsNumber = idNumber.Length == 10;
        bool isSaudiNumber = isTenDigitsNumber && (idNumber[0] == '1' || idNumber[0] == '2');
        return !isSaudiNumber;
    }


}

