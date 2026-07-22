using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Extensions;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using Serilog;
using Serilog.Context;
using Serilog.Core;

var builder = Host.CreateApplicationBuilder(args);

builder.AddSerilogLogging();

builder.Services.AddDbContext<ActivationDbContext>(opts =>
    opts.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sqlOpts => sqlOpts.UseCompatibilityLevel(120)));

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var dbContext = host.Services.GetRequiredService<ActivationDbContext>();

string fixOutage = "Data/FixOutage.txt";


List<int> execludedNotificaitonCode = [12,13,14,15];
List<int> execludedStep = [2, 4, 6, 8, 10, 12];
List<int> requestTypeNeedTermination = [1, 5, 17, 18];
List<string> successCode = ["600"];
List<string> failedCode = ["1", "715"];


try
{
    LogContext.PushProperty("ProcessName", "FixOutage");

    var fixOutageNotificationIds = ReadIds(fixOutage);
    var notifications = dbContext.SematiNotifications.AsNoTracking().Where(x => fixOutageNotificationIds.Contains(x.Id) && !execludedNotificaitonCode.Contains(x.NotificationCode)).ToList();

    foreach(var notification in notifications)
    {
        var notificationActions = dbContext.SematiNotificationActions.AsNoTracking().Where(x => x.SematiNotificationId == notification.Id && !execludedStep.Contains(x.SematiNotificationActionStepId)).ToList();
        var personId = notification.IdNumber;

        using var __ = LogContext.PushProperty("NotificationId", notification.Id);
        using var ___ = LogContext.PushProperty("PersonId", personId);

        foreach (var action in notificationActions) 
        {
            try
            {
                using var _ = LogContext.PushProperty("ActionId", action.Id);
                using var ____ = LogContext.PushProperty("MSISDN", action.MSISDN);


                if (action.SematiUpdateCode == "600" || action.SematiUpdateCode == "780" || action.SematiUpdateCode == "606")
                {
                    logger.LogWarning("Action {ActionId} has SematiUpdateCode {SematiUpdateCode}  — skipping", action.Id, action.SematiUpdateCode);
                    continue;
                }

                var rawCallReports = await dbContext.SematiCallReports.AsNoTracking()
                                                    .Where(x => x.msisdn == action.MSISDN && (x.personId == personId || x.oldOwnerId == personId))
                                                    .OrderByDescending(x => x.TimeStamp)
                                                    .ToListAsync();

                var latestSuccess = rawCallReports.Where(x => x.code is not null && successCode.Contains(x.code)).OrderByDescending(x => x.TimeStamp).FirstOrDefault();

                if (latestSuccess == null || !(latestSuccess.requestType.HasValue && requestTypeNeedTermination.Contains(latestSuccess.requestType.Value)))
                {
                    logger.LogWarning("skipping - no success call report found");
                    continue;
                }

                var callReportAfterLatestSuccess = rawCallReports.Where(x => x.TimeStamp > latestSuccess.TimeStamp
                                                                             && x.requestType == 4
                                                                             && x.personId == personId
                                                                             && x.code is not null
                                                                             && failedCode.Contains(x.code)).ToList();

                if (callReportAfterLatestSuccess.Count == 0)
                {
                    logger.LogWarning("skipping - no failed termination Semati call report after lates success");
                    continue;
                }

                logger.LogInformation("add termination record for MSISDN {MSISDN}, and IDNumber {IDNumber}", action.MSISDN, personId);
                var terminateNumber = new SematiTerminateNumber
                {
                    MSISDN = action.MSISDN,
                    IDNumber = personId,
                    IDTypeID = GetIdTypeByPersonId(personId),
                    SematiCode = -1,
                    NationalityID = 113,
                    SubscriptionType = "V"
                };

                await dbContext.SematiTerminateNumbers.AddAsync(terminateNumber);
                await dbContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unhandled exception during processing");
            }
        }
    }
}
catch (Exception ex)
{
    logger.LogError(ex, "Unhandled exception during processing");
}
finally
{
    await Log.CloseAndFlushAsync();
}

static List<int> ReadIds(string path)
{
    if (!File.Exists(path)) return [];
    return File.ReadAllLines(path)
        .Select(line => int.TryParse(line.Trim(), out var id) ? id : (int?)null)
        .Where(id => id.HasValue)
        .Select(id => id!.Value)
        .ToList();
}

static byte GetIdTypeByPersonId(string s)
{
    if (string.IsNullOrEmpty(s)) return 3;
    var a = s[0];
    if (a == '1') return 1;
    if (a == '2') return 2;
    return 3;
}

