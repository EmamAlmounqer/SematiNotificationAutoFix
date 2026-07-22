using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Enums;
using SematiNotificationAutoFix.Console.Models;
using SematiNotificationAutoFix.Console.Services;
using SematiNotificationAutoFix.DAL.Data;
using Serilog.Context;

namespace SematiNotificationAutoFix.Console.Processes;

public class MissingSematiTermination
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<MissingSematiTermination> _logger;
    private readonly TerminationService _terminationProcess;
    private readonly NumberService _numberService;

    public MissingSematiTermination(ActivationDbContext dbContext, ILogger<MissingSematiTermination> logger, TerminationService terminationProcess, NumberService numberService)
    {
        _dbContext = dbContext;
        _logger = logger;
        _numberService = numberService;
        _terminationProcess = terminationProcess;
    }

    public async Task<List<int>> ProcessAsync(List<int> actionIds)
    {
        var successfulIds = new List<int>();
        foreach (var id in actionIds)
        {
            try
            {
                if (await ProcessAsync(id)) 
                    successfulIds.Add(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception processing action {ActionId}", id);
            }
        }
        return successfulIds;
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

        using var ___ = LogContext.PushProperty("NotificationId", action.SematiNotificationId);

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

        var needTermination = await _numberService.DoNumberNeedTerminationForPersonId(personId, action.MSISDN);

        if (!needTermination)
        {
            _logger.LogWarning("No termination needed for action: {ActionId} (MSISDN={MSISDN}, PersonId={PersonId}) — skipping", sematiNotificationActionId, action.MSISDN, personId);
            return false;
        }

        var terminationResult = await _terminationProcess.TerminateAndSaveAsync(action.MSISDN, personId);
        if (!terminationResult.IsTerminationSuccess())
        {
            _logger.LogError("Failed to terminate number for action {ActionId} (MSISDN={MSISDN}, PersonId={PersonId})", sematiNotificationActionId, action.MSISDN, personId);
            return false;
        }

        return true;
    }
}