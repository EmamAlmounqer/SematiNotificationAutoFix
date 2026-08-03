
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using Serilog.Context;

namespace SematiNotificationAutoFix.Console.Processes;

public class FixOutageProcess
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<Fix606Process> _logger;

    private static readonly List<int> execludedNotificaitonCode = [12, 13, 14, 15];
    private static readonly List<int> execludedStep = [2, 4, 6, 8, 10, 12];
    private static readonly List<int> requestTypeNeedTermination = [1, 5, 17, 18];
    private static readonly List<string> successCode = ["600"];
    private static readonly List<string> failedCode = ["1", "715"];

    public FixOutageProcess(ActivationDbContext dbContext, ILogger<Fix606Process> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    /// <summary>
    /// Only add to SematiTerminateNumber for notificaiton that has Outage record in SematiCallReports
    /// </summary>
    public async Task ProcessAsync(List<int> fixOutageNotificationIds)
    {
        using var _ = LogContext.PushProperty("ProcessName", "FixOutage");

        var notifications = _dbContext.SematiNotifications.AsNoTracking().Where(x => fixOutageNotificationIds.Contains(x.Id) && !execludedNotificaitonCode.Contains(x.NotificationCode)).ToList();

        foreach (var notification in notifications)
        {
            var notificationActions = _dbContext.SematiNotificationActions.AsNoTracking().Where(x => x.SematiNotificationId == notification.Id && !execludedStep.Contains(x.SematiNotificationActionStepId)).ToList();
            var personId = notification.IdNumber;

            using var __ = LogContext.PushProperty("NotificationId", notification.Id);
            using var ___ = LogContext.PushProperty("PersonId", personId);

            foreach (var action in notificationActions)
            {
                try
                {
                    using var ____ = LogContext.PushProperty("ActionId", action.Id);
                    using var _____ = LogContext.PushProperty("MSISDN", action.MSISDN);


                    if (action.SematiUpdateCode == "600" || action.SematiUpdateCode == "780" || action.SematiUpdateCode == "606")
                    {
                        _logger.LogWarning("Action {ActionId} has SematiUpdateCode {SematiUpdateCode}  — skipping", action.Id, action.SematiUpdateCode);
                        continue;
                    }

                    var rawCallReports = await _dbContext.SematiCallReports.AsNoTracking()
                                                        .Where(x => x.msisdn == action.MSISDN && (x.personId == personId || x.oldOwnerId == personId))
                                                        .OrderByDescending(x => x.TimeStamp)
                                                        .ToListAsync();

                    var latestSuccess = rawCallReports.Where(x => x.code is not null && successCode.Contains(x.code)).OrderByDescending(x => x.TimeStamp).FirstOrDefault();

                    if (latestSuccess == null || !(latestSuccess.requestType.HasValue && requestTypeNeedTermination.Contains(latestSuccess.requestType.Value)))
                    {
                        _logger.LogWarning("skipping - no success call report found");
                        continue;
                    }

                    var callReportAfterLatestSuccess = rawCallReports.Where(x => x.TimeStamp > latestSuccess.TimeStamp
                                                                                    && x.requestType == 4
                                                                                    && x.personId == personId
                                                                                    && x.code is not null
                                                                                    && failedCode.Contains(x.code)).ToList();

                    if (callReportAfterLatestSuccess.Count == 0)
                    {
                        _logger.LogWarning("skipping - no failed termination Semati call report after latest success");
                        continue;
                    }

                    _logger.LogInformation("add termination record for MSISDN {MSISDN}, and IDNumber {IDNumber}", action.MSISDN, personId);
                    var terminateNumber = new SematiTerminateNumber
                    {
                        MSISDN = action.MSISDN,
                        IDNumber = personId,
                        IDTypeID = GetIdTypeByPersonId(personId),
                        SematiCode = -1,
                        NationalityID = 113,
                        SubscriptionType = "V"
                    };

                    await _dbContext.SematiTerminateNumbers.AddAsync(terminateNumber);
                    await _dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unhandled exception during processing");
                }
            }
        }


        static byte GetIdTypeByPersonId(string s)
        {
            if (string.IsNullOrEmpty(s)) return 3;
            var a = s[0];
            if (a == '1') return 1;
            if (a == '2') return 2;
            return 3;
        }


    }

}

