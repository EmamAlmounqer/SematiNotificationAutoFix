using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.DAL.Data;
using Serilog.Context;

namespace SematiNotificationAutoFix.Console.Processes;

public class MissingSematiTermination
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<MissingSematiTermination> _logger;
    private readonly TerminationProcess _terminationProcess;

    public MissingSematiTermination(ActivationDbContext dbContext, ILogger<MissingSematiTermination> logger, TerminationProcess terminationProcess)
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

        var action = await _dbContext.SematiNotificationActions.AsNoTracking().Include(x => x.SematiNotification)
                                                               .AsNoTracking()
                                                               .FirstOrDefaultAsync(x => x.Id == sematiNotificationActionId);
        if (action is null)
        {
            _logger.LogWarning("Action {ActionId} not found — skipping", sematiNotificationActionId);
            return;
        }

        if (action.SematiUpdateCode == "600" || action.SematiUpdateCode == "780")
        {
            _logger.LogWarning("Action {ActionId} has SematiUpdateCode {SematiUpdateCode}  — skipping", sematiNotificationActionId, action.SematiUpdateCode);
            return;
        }

        var personId = action.SematiNotification.IdNumber;
        if (personId is null)
        {
            _logger.LogWarning("No PersonId found for action {ActionId} — skipping", sematiNotificationActionId);
            return;
        }

        using var ___ = LogContext.PushProperty("PersonId", personId);

        var callReports = await _dbContext.SematiCallReports.AsNoTracking()
                                                                  .Where(x => x.msisdn == action.MSISDN
                                                                              && x.code == "600"
                                                                              && x.personId == personId)
                                                                  .OrderByDescending(x => x.TimeStamp)
                                                                  .FirstOrDefaultAsync();
                                                                  
        if (callReports is null)
        {
            _logger.LogWarning("No code-600 SematiCallReport found for action {ActionId} (MSISDN={MSISDN}, PersonId={PersonId}) — skipping", sematiNotificationActionId, action.MSISDN, personId);
            return;
        }

        if (callReports.requestType != 1)
        {
            _logger.LogWarning("request type in SematiCallLogID (id={id}) is not type 1 it is type {requestType}", callReports.SematiCallLogID, callReports.requestType);
            return;
        }

        await _terminationProcess.TerminateAndSave(action.MSISDN, personId, sematiNotificationActionId);
    }
}