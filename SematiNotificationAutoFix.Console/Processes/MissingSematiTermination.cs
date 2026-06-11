using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using Serilog.Context;

namespace SematiNotificationAutoFix.Console.Processes;

public class MissingSematiTermination
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<Fix606Process> _logger;
    private readonly TerminationProcess _terminationProcess;

    public MissingSematiTermination(IConfiguration configuration, ActivationDbContext dbContext, ILogger<Fix606Process> logger, TerminationProcess terminationProcess)
    {
        _dbContext = dbContext;
        _logger = logger;
        _terminationProcess = terminationProcess;
    }

    public async Task Process(int sematiNotificationActionId)
    {
        using var _ = LogContext.PushProperty("ProcessName", "MissingSematiTermination");
        using var __ = LogContext.PushProperty("ActionId", sematiNotificationActionId);

        _logger.LogInformation("Processing action {ActionId}", sematiNotificationActionId);

        var sematiNotificationAction = await _dbContext.SematiNotificationActions.FirstOrDefaultAsync(x => x.Id == sematiNotificationActionId);
        if (sematiNotificationAction?.SematiUpdateTcn is null)
        {
            _logger.LogWarning("Action {ActionId} not found or has no TCN — skipping", sematiNotificationActionId);
            return;
        }

        var personId = sematiNotificationAction.SematiNotification.IdNumber;
        if (personId is null)
        {
            _logger.LogWarning("Could not Find PeronsId For Action {SematiNotificationActionId}", sematiNotificationActionId);
            return;
        }

        using var ___ = LogContext.PushProperty("UpdateTcn", sematiNotificationAction.SematiUpdateTcn);
        using var ____ = LogContext.PushProperty("PersonId", personId);

        var sematiCallReports = await _dbContext.SematiCallReports.Where(x => x.msisdn == sematiNotificationAction.MSISDN && x.code == "600" && x.personId == personId).OrderByDescending(x => x.TimeStamp).FirstOrDefaultAsync();
        if (sematiCallReports is null || sematiCallReports.code != "600")
        {
            _logger.LogWarning("No 600 sematiCallReports found for action {ActionId} (TCN={Tcn}) — skipping", sematiNotificationActionId, sematiNotificationAction.SematiUpdateTcn);
            return;
        }

        if (sematiCallReports.requestType != 1)
        {
            _logger.LogWarning("request type in SematiCallLogID (id={id}) is not type 1 it is type {requestType}", sematiCallReports.SematiCallLogID, sematiCallReports.requestType);
            return;
        }

        _logger.LogInformation("Start SematiTerminateNumber {MSISDN} terminate numbers for action {ActionId} (PersonId={PersonId})", sematiNotificationAction.MSISDN, sematiNotificationActionId, personId);

        var terminateNumber = new SematiTerminateNumber
        {
            MSISDN = sematiNotificationAction.MSISDN,
            IDNumber = personId,
            IDTypeID = GetIdTypeID(personId),
            SematiCode = -2,
            NationalityID = 113,
            SubscriptionType = "V"
        };

        await _terminationProcess.TerminateNumber(terminateNumber);
        await _dbContext.SematiTerminateNumbers.AddAsync(terminateNumber);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("End SematiTerminateNumber {MSISDN} with SematiTerminateNumberId {id}", sematiNotificationAction.MSISDN, terminateNumber);
    }

    private byte GetIdTypeID(string s)
    {
        if (string.IsNullOrEmpty(s)) return 3;
        var a = s[0];
        if (a == '1') return 1;
        if (a == '2') return 2;
        return 3;
    }
}
