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
    private readonly ILogger<MissingSematiTermination> _logger;
    private readonly TerminationProcess _terminationProcess;

    public MissingSematiTermination(IConfiguration configuration, ActivationDbContext dbContext, ILogger<MissingSematiTermination> logger, TerminationProcess terminationProcess)
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
            _logger.LogWarning("No PersonId found for action {ActionId} — skipping", sematiNotificationActionId);
            return;
        }

        using var ___ = LogContext.PushProperty("PersonId", personId);

        var sematiCallReports = await _dbContext.SematiCallReports.Where(x => x.msisdn == sematiNotificationAction.MSISDN && x.code == "600" && x.personId == personId).OrderByDescending(x => x.TimeStamp).FirstOrDefaultAsync();
        if (sematiCallReports is null)
        {
            _logger.LogWarning("No code-600 SematiCallReport found for action {ActionId} (MSISDN={MSISDN}, PersonId={PersonId}) — skipping", sematiNotificationActionId, sematiNotificationAction.MSISDN, personId);
            return;
        }

        if (sematiCallReports.requestType != 1)
        {
            _logger.LogWarning("request type in SematiCallLogID (id={id}) is not type 1 it is type {requestType}", sematiCallReports.SematiCallLogID, sematiCallReports.requestType);
            return;
        }

        _logger.LogInformation("Terminating number {MSISDN} for action {ActionId} (PersonId={PersonId})", sematiNotificationAction.MSISDN, sematiNotificationActionId, personId);

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

        _logger.LogInformation("Terminated number {MSISDN} — SematiTerminateNumberId={Id}", sematiNotificationAction.MSISDN, terminateNumber.ID);
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