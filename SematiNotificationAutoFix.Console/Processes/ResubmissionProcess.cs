using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using Serilog.Context;

namespace SematiNotificationAutoFix.Console.Processes;

public class ResubmissionProcess
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<ResubmissionProcess> _logger;

    public ResubmissionProcess(ActivationDbContext dbContext, ILogger<ResubmissionProcess> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task ResubmitAsync(List<int> actionIds)
    {
        using var _ = LogContext.PushProperty("ProcessName", "Resubmission");

        _logger.LogInformation("Resubmitting {Count} actions: {@Ids}", actionIds.Count, actionIds);

        var updatedIds = await UpdateActionsAsync(actionIds);

        var delayTimeInSecond = 20;
        _logger.LogInformation("Waiting {DelayTime} seconds before checking results", delayTimeInSecond);
        await Task.Delay(TimeSpan.FromSeconds(delayTimeInSecond));

        await UpdateNotificationStatusAsync(updatedIds);
    }

    private async Task<List<int>> UpdateActionsAsync(List<int> ids)
    {
        List<int> updatedIds = [];
        var now = DateTime.Now;
        var actions = await _dbContext.SematiNotificationActions
            .Where(x => ids.Contains(x.Id))
            .ToListAsync();

        foreach (var action in actions)
        {
            using var __ = LogContext.PushProperty("ActionId", action.Id);

            if (action.SematiUpdateCode == "600" || action.SematiUpdateCode == "780")
            {
                _logger.LogWarning("Action {ActionId} has SematiUpdateCode {SematiUpdateCode}  — skipping", action.Id, action.SematiUpdateCode);
                continue;
            }

            action.Status = 2;
            action.RetriesCount = null;

            if (action.ExpectedActionDate > now)
                action.ExpectedActionDate = now;

            _logger.LogInformation("Resubmitting action {ActionId} (Status=2, RetriesCount=null, ExpectedActionDate={ExpectedActionDate})", action.Id, action.ExpectedActionDate);
            updatedIds.Add(action.Id);
        }

        await _dbContext.SaveChangesAsync();
        return updatedIds;
    }

    private async Task UpdateNotificationStatusAsync(List<int> ids)
    {
        _dbContext.ChangeTracker.Clear();

        var actions = await _dbContext.SematiNotificationActions
            .Where(x => ids.Contains(x.Id))
            .Include(x => x.SematiNotification)
            .ToListAsync();

        foreach (var action in actions)
        {
            using var __ = LogContext.PushProperty("ActionId", action.Id);

            if (action.SematiUpdateCode != "600" && action.SematiUpdateCode != "780")
            {
                _logger.LogInformation("Action {ActionId} has code {Code} — no notification status update needed", action.Id, action.SematiUpdateCode);
                continue;
            }

            action.SematiNotification.Status = 3;
            _logger.LogInformation("Setting SematiNotification {NotificationId} Status=3 (Action={ActionId}, Code={Code})", action.SematiNotificationId, action.Id, action.SematiUpdateCode);
        }

        await _dbContext.SaveChangesAsync();
    }
}
