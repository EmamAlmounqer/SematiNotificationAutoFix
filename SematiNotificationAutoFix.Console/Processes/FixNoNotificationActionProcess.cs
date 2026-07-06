using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NobillCalls;
using SematiNotificationAutoFix.Console.Enums;
using SematiNotificationAutoFix.Console.Models;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using Serilog.Context;

namespace SematiNotificationAutoFix.Console.Processes;

public class FixNoNotificationActionProcess
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<Fix606Process> _logger;
    private readonly TerminationProcess _terminationProcess;
    private readonly int[] _requestTypeNeedAction = [RequestType.NewActivation.GetHashCode()];
    private readonly NobillServiceClient _nobill;

    public FixNoNotificationActionProcess(ActivationDbContext dbContext, ILogger<Fix606Process> logger, TerminationProcess terminationProcess, NobillServiceClient nobill)
    {
        _dbContext = dbContext;
        _logger = logger;
        _terminationProcess = terminationProcess;
        _nobill = nobill;
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

        var personId = notification.IdNumber;
        using var ___ = LogContext.PushProperty("PersonId", personId);

        var (msisdns, nobillAccountNumber) = await FetchMsisdnsAndAccountNumber(notification, personId);

        List<SematiNotificationAction> newActions = [];
        foreach (var msisdn in msisdns)
        {
            using var ____ = LogContext.PushProperty("MSISDN", msisdn);
            var terminationResult = await _terminationProcess.TerminateAndSaveAsync(msisdn, personId);

            if (!terminationResult.IsTerminationSuccess())
            {
                _logger.LogWarning("Termination Form {MSISDN} has ResponseCode {ResponseCode} — skipping", msisdn, terminationResult.ResponseCode);
                continue;
            }

            var newAction = new SematiNotificationAction
            {
                MSISDN = msisdn,
                ExpectedActionDate = DateTime.Now,
                CreatedAt = DateTime.Now,
                Status = (int)SematiNotificationStatus.HasErrors,
                SematiNotificationId = notification.Id,
                SematiNotificationActionStepId = sematiNotificationActionStepId,
                AccountNumber = nobillAccountNumber
            };

            newActions.Add(newAction);
            _logger.LogInformation("Created new SematiNotificationAction for notification {NotificationId} with MSISDN {MSISDN} and AccountNumber {AccountNumber}", notification.Id, msisdn, nobillAccountNumber);
        }

        _dbContext.SematiNotificationActions.AddRange(newActions);
        await _dbContext.SaveChangesAsync();

        var newActionIds = newActions.Select(a => a.Id).ToList();
        _logger.LogInformation("Saved {Count} new actions for notification {NotificationId} with AccountNumber {AccountNumber} — IDs: {@Ids}, MSISDNs: {@MSISDNs}",
            newActions.Count,
            notification.Id,
            nobillAccountNumber,
            newActionIds,
            newActions.Select(a => a.MSISDN));

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

    public static bool IsBorderNumber(string? number)
    {
        if (number == null) { return false; }

        bool isTenDigitsNumber = number.Length == 10;
        bool isSaudiNumber = isTenDigitsNumber && (number[0] == '1' || number[0] == '2');
        return !isSaudiNumber;
    }

    private async Task<(List<string> Msisdns, string? nobillAccountNumber)> FetchMsisdnsAndAccountNumber(SematiNotification notification, string personId)
    {

        var identityMaster = await _dbContext.IdentityMasters.AsNoTracking().FirstOrDefaultAsync(x => x.BorderNumber == personId)
                   ?? await _dbContext.IdentityMasters.AsNoTracking().FirstOrDefaultAsync(x => x.IdNumber == personId);

        if (identityMaster is not null)
        {
            var activations = await _dbContext.Activations.AsNoTracking()
                                                 .Where(x => x.IdentityMasterId == identityMaster.Id)
                                                 .OrderByDescending(x => x.CreatedOn)
                                                 .ToListAsync();

            if (activations is not null && activations.Count != 0)
            {
                var msisdns = activations.Select(x => x.MSISDN).ToList();
                var nobillAccountNumber = activations.FirstOrDefault()?.NobillAccountNumber;
                return (msisdns, nobillAccountNumber);
            }
            else
            {
                _logger.LogWarning("No Activation found for notification {NotificationId} with personId {PersonId} and IdentityMasterId {IdentityMasterId}", notification.Id, personId, identityMaster.Id);
            }
        }
        else
        {
            _logger.LogWarning("No IdentityMaster found for notification {NotificationId} with personId {PersonId}", notification.Id, personId);
        }

        var rawCallReports = await _dbContext.SematiCallReports.AsNoTracking()
                                                              .Where(x => x.code == "600" && x.personId == personId && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToListAsync();


        if (rawCallReports is null || rawCallReports.Count == 0)
        {
            _logger.LogWarning("No CallReport with code 600 found for notification {NotificationId} with personId {PersonId}", notification.Id, personId);
            return ([], null);
        }

        var callReports = rawCallReports.GroupBy(x => x.msisdn!)
                                .ToDictionary(g => g.Key, g => g.ToList());

        if (callReports.Count == 0)
        {
            _logger.LogWarning("No CallReport with code 600 containing MSISDN found for notification {NotificationId} with personId {PersonId}", notification.Id, personId);
            return ([], null);
        }

        var msisdnList = callReports.Keys.ToList();
        foreach (var (msisdn, reports) in callReports)
        {
            var sortedReports = reports.OrderByDescending(x => x.TimeStamp).ToList();
            var latestReport = sortedReports.FirstOrDefault();
            if (latestReport?.requestType is not null && _requestTypeNeedAction.Contains(latestReport.requestType.Value))
            {
                msisdnList.Add(msisdn);
            }
            else
            {
                _logger.LogInformation("Latest CallReport with code 600 for MSISDN {MSISDN} in notification {NotificationId} with personId {PersonId} has requestType {RequestType}", msisdn, notification.Id, personId, latestReport?.requestType);
            }
        }

        var accountData = await _nobill.GetCustomerDataAsync(msisdnList.FirstOrDefault());
        var accountNumber = accountData?.Body?.details?.CustomerNum;
        var customerId = accountData?.Body?.details?.CustomerID;

        if (string.IsNullOrEmpty(accountNumber))
        {
            _logger.LogWarning("No Nobill account number found for MSISDN {MSISDN} from CallReport for notification {NotificationId}", msisdnList.FirstOrDefault(), notification.Id);
            return ([], null);
        }

        if (customerId != personId)
        {
            _logger.LogWarning("Nobill account data found for MSISDN {MSISDN} from CallReport for notification {NotificationId} does not match personId {PersonId}", msisdnList.FirstOrDefault(), notification.Id, personId);   
            return ([], null);
        }


        return (msisdnList, accountNumber);
    }
}
