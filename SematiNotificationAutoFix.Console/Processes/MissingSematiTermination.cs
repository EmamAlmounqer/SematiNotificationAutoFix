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

    public async Task<List<int>> ProcessAsync(List<int> actionIds)
    {
        var sucessfulIds = new List<int>();
        foreach (var id in actionIds)
        {
            try
            {
                if (await ProcessAsync(id)) 
                    sucessfulIds.Add(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception processing action {ActionId}", id);
            }
        }
        return sucessfulIds;
    }

    public async Task<bool> ProcessAsync(int sematiNotificationActionId)
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
            return false;
        }

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
            return false;
        }

        if (callReports.requestType != 1)
        {
            _logger.LogWarning("request type in SematiCallLogID (id={id}) is not type 1 it is type {requestType}", callReports.SematiCallLogID, callReports.requestType);
            return false;
        }

        if (!await _terminationProcess.TerminateAndSaveAsync(action.MSISDN, personId, sematiNotificationActionId))
        {
            _logger.LogError("Failed to terminate number for action {ActionId} (MSISDN={MSISDN}, PersonId={PersonId})", sematiNotificationActionId, action.MSISDN, personId);
            return false;
        }

        return true;
    }
}