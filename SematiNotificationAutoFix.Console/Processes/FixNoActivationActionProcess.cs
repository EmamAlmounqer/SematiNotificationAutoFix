using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NobillCalls;
using SematiNotificationAutoFix.Console.Enums;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using Serilog.Context;

namespace SematiNotificationAutoFix.Console.Processes;

public class FixNoActivationActionProcess
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<Fix606Process> _logger;
    private readonly TerminationProcess _terminationProcess;
    private readonly NobillServiceClient _nobill;

    public FixNoActivationActionProcess(ActivationDbContext dbContext, ILogger<Fix606Process> logger, TerminationProcess terminationProcess, NobillServiceClient nobill)
    {
        _dbContext = dbContext;
        _logger = logger;
        _terminationProcess = terminationProcess;
        _nobill = nobill;
    }

    public async Task<List<int>> TerminateAsync(List<int> sematiNotificationIds)
    {
        LogContext.PushProperty("ProcessName", "FixNoActivationAction");
        var terminatedIds = new List<int>();

        var notifications = await _dbContext.SematiNotifications.AsNoTracking()
                                                                .Where(x => sematiNotificationIds.Contains(x.Id))
                                                                .ToListAsync();

        foreach (var notification in notifications)
        {
            try
            {
                if (await TerminateAsync(notification))
                    terminatedIds.Add(notification.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception terminating Semati Notification {SematiNotificationId}", notification.Id);
            }
        }

        return terminatedIds;
    }

    public async Task SaveActionsAsync(List<int> sematiNotificationIds)
    {
        LogContext.PushProperty("ProcessName", "FixNoActivationAction");

        var notifications = await _dbContext.SematiNotifications.AsNoTracking()
                                                                .Where(x => sematiNotificationIds.Contains(x.Id))
                                                                .ToListAsync();

        foreach (var notification in notifications)
        {
            try
            {
                await SaveActionAsync(notification);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception saving action for Semati Notification {SematiNotificationId}", notification.Id);
            }
        }
    }

    private async Task<bool> TerminateAsync(SematiNotification notification)
    {
        LogContext.PushProperty("NotificationId", notification.Id);

        if (_dbContext.SematiNotificationActions.Any(x => x.SematiNotificationId == notification.Id))
        {
            _logger.LogWarning("Notification {NotificationId} already has an action associated with it.", notification.Id);
            return false;
        }

        var sematiNotificationActionStepId = GetSematiNotificationActionStepIdByCode(notification.NotificationCode);
        if (sematiNotificationActionStepId == -1)
        {
            _logger.LogWarning("No SematiNotificationActionStep mapping found for notification code {NotificationCode} for notification {NotificationId}", notification.NotificationCode, notification.Id);
            return false;
        }

        var personId = notification.IdNumber;

        var (msisdns, nobillAccountNumber) = await FetchMsisdnsAndAccountNumber(notification, personId);

        foreach (var msisdn in msisdns)
        {
            await _terminationProcess.TerminateAndSaveAsync(msisdn, personId);

            // should be after running the job
            var newAction = new SematiNotificationAction
            {
                MSISDN = msisdn,
                ExpectedActionDate = DateTime.Now,
                CreatedAt = DateTime.Now,
                Status = 2,
                SematiNotificationId = notification.Id,
                SematiNotificationActionStepId = sematiNotificationActionStepId,
                AccountNumber = nobillAccountNumber
            };

            _dbContext.SematiNotificationActions.Add(newAction);
            _logger.LogInformation("Created new activation action for notification {NotificationId} with MSISDN {MSISDN} and AccountNumber {AccountNumber}", notification.Id, msisdn, nobillAccountNumber);

        }

        //notification.Status = (int)SematiNotificationStatus.Completed;
        await _dbContext.SaveChangesAsync();

        return true;
    }

    private async Task SaveActionAsync(SematiNotification notification)
    {
        LogContext.PushProperty("NotificationId", notification.Id);

        if (_dbContext.SematiNotificationActions.Any(x => x.SematiNotificationId == notification.Id))
        {
            _logger.LogWarning("Notification {NotificationId} already has an action associated with it, skipping action creation.", notification.Id);
            return;
        }

        var sematiNotificationActionStepId = GetSematiNotificationActionStepIdByCode(notification.NotificationCode);
        if (sematiNotificationActionStepId == -1)
        {
            _logger.LogWarning("No SematiNotificationActionStep mapping found for notification code {NotificationCode} for notification {NotificationId}", notification.NotificationCode, notification.Id);
            return;
        }

        var personId = notification.IdNumber;
        var identityMaster = await _dbContext.IdentityMasters.AsNoTracking().FirstOrDefaultAsync(x => x.BorderNumber == personId)
                           ?? await _dbContext.IdentityMasters.AsNoTracking().FirstOrDefaultAsync(x => x.IdNumber == personId);

        if (identityMaster is null)
        {
            _logger.LogWarning("No IdentityMaster found for notification {NotificationId} with personId {PersonId}", notification.Id, personId);
            return;
        }

        var activations = await _dbContext.Activations.AsNoTracking()
                                                     .Where(x => x.IdentityMasterId == identityMaster.Id)
                                                     .OrderByDescending(x => x.CreatedOn)
                                                     .ToListAsync();

        if (activations is null || activations.Count == 0)
        {
            _logger.LogWarning("No Activation found for notification {NotificationId} with personId {PersonId} and IdentityMasterId {IdentityMasterId}", notification.Id, personId, identityMaster.Id);
            return;
        }

        if (activations.Count > 1)
        {
            _logger.LogWarning("Multiple Activations found for notification {NotificationId} with personId {PersonId} and IdentityMasterId {IdentityMasterId}.", notification.Id, personId, identityMaster.Id);
            return;
        }

        var activation = activations[0];
        var newAction = new SematiNotificationAction
        {
            MSISDN = activation.MSISDN,
            ExpectedActionDate = DateTime.Now,
            CreatedAt = DateTime.Now,
            Status = 2,
            SematiNotificationId = notification.Id,
            SematiNotificationActionStepId = sematiNotificationActionStepId,
            AccountNumber = activation.NobillAccountNumber
        };

        _dbContext.SematiNotificationActions.Add(newAction);
        _logger.LogInformation("Created new activation action for notification {NotificationId} with MSISDN {MSISDN} and AccountNumber {AccountNumber}", notification.Id, activation.MSISDN, activation.NobillAccountNumber);

        //notification.Status = (int)SematiNotificationStatus.Completed;
        await _dbContext.SaveChangesAsync();
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

    private async Task<(List<string> Msisdns, string? nobillAccoundNumber)> FetchMsisdnsAndAccountNumber(SematiNotification notification, string personId)
    {

        var identityMaster = await _dbContext.IdentityMasters.AsNoTracking().FirstOrDefaultAsync(x => x.BorderNumber == personId)
                   ?? await _dbContext.IdentityMasters.AsNoTracking().FirstOrDefaultAsync(x => x.IdNumber == personId);

        if (identityMaster is not null)
        {

            var activations = await _dbContext.Activations.AsNoTracking()
                                                 .Where(x => x.IdentityMasterId == identityMaster.Id)
                                                 .OrderByDescending(x => x.CreatedOn)
                                                 .ToListAsync();

            if (activations is null || activations.Count == 0)
            {
                _logger.LogWarning("No Activation found for notification {NotificationId} with personId {PersonId} and IdentityMasterId {IdentityMasterId}", notification.Id, personId, identityMaster.Id);
            } 
            else
            {
                var msisdns = activations.Select(x => x.MSISDN).ToList();
                var nobillAccountNumber = activations.FirstOrDefault()?.NobillAccountNumber;
                return (msisdns, nobillAccountNumber);
            }


        }
        else
        {
            _logger.LogWarning("No IdentityMaster found for notification {NotificationId} with personId {PersonId}", notification.Id, personId);
        }

        var callReports = await _dbContext.SematiCallReports.AsNoTracking()
                                                          .Where(x => x.code == "600" && x.personId == personId)
                                                          .OrderByDescending(x => x.TimeStamp)
                                                          .FirstOrDefaultAsync();

        if (callReports is null)
        {
            _logger.LogWarning("No CallReport with code 600 found for notification {NotificationId} with personId {PersonId}", notification.Id, personId);
            return ([], null);
        }

        if (string.IsNullOrEmpty(callReports.msisdn))
        {
            _logger.LogWarning("CallReport with code 600 for notification {NotificationId} with personId {PersonId} does not have an MSISDN", notification.Id, personId);
            return ([], null);
        }

        var accountData = await _nobill.GetAccountDataAsync(callReports.msisdn);
        var accountNumber = accountData.Body.details.AccountNum;

        if (string.IsNullOrEmpty(accountNumber))
        {
            _logger.LogWarning("No Nobill account number found for MSISDN {MSISDN} from CallReport for notification {NotificationId}", callReports.msisdn, notification.Id);
            return ([], null);
        }

        return ([callReports.msisdn], accountNumber);
    }
}
