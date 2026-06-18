using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Enums;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using Serilog.Context;

namespace SematiNotificationAutoFix.Console.Processes;

public class FixNoActivationActionProcess
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<Fix606Process> _logger;


    public FixNoActivationActionProcess(ActivationDbContext dbContext, ILogger<Fix606Process> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<List<int>> ProcessAsync(List<int> sematiNotificationIds)
    {
        LogContext.PushProperty("ProcessName", "FixNoActivationAction");
        var fixedIds = new List<int>();

        var notifications = await _dbContext.SematiNotifications.AsNoTracking()
                                                                .Where(x => sematiNotificationIds.Contains(x.Id))
                                                                .ToListAsync();

        foreach (var notification in notifications)
        {
            try
            {
                if (await ProcessAsync(notification))
                    fixedIds.Add(notification.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, message: "Unhandled exception processing Semati Notification {SematiNotificationId}", notification.Id);
            }

        }

        return fixedIds;
    }

    private async Task<bool> ProcessAsync(SematiNotification notification)
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
        var identityMaster = await _dbContext.IdentityMasters.AsNoTracking().FirstOrDefaultAsync(x => x.BorderNumber == personId)
                           ?? await _dbContext.IdentityMasters.AsNoTracking().FirstOrDefaultAsync(x => x.IdNumber == personId);

        if (identityMaster is null)
        {
            _logger.LogWarning("No IdentityMaster found for notification {NotificationId} with personId {PersonId}", notification.Id, personId);
            return false;
        }

        var activations = await _dbContext.Activations.AsNoTracking()
                                                     .Where(x => x.IdentityMasterId == identityMaster.Id)
                                                     .OrderByDescending(x => x.CreatedOn)
                                                     .ToListAsync();


        if (activations is null || activations.Count == 0)
        {
            _logger.LogWarning("No Activation found for notification {NotificationId} with personId {PersonId} and IdentityMasterId {IdentityMasterId}", notification.Id, personId, identityMaster.Id);
            return false;
        }

        if (activations.Count > 1)
        {
            _logger.LogWarning("Multiple Activations found for notification {NotificationId} with personId {PersonId} and IdentityMasterId {IdentityMasterId}.", notification.Id, personId, identityMaster.Id);
            return false;
        }

        foreach (var activation in activations)
        {
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
        }

        //notification.Status = (int)SematiNotificationStatus.Completed;
        await _dbContext.SaveChangesAsync();
        return true;
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
}
