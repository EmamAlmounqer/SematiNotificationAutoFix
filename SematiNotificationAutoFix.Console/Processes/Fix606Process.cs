using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Models;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using Serilog.Context;
using System.Text.Json;

namespace SematiNotificationAutoFix.Console.Processes;

public class Fix606Process
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<Fix606Process> _logger;
    private readonly TerminationProcess _terminationProcess;
    private readonly int _sematiServiceCallLogCutOffId;

    public Fix606Process(IConfiguration configuration, ActivationDbContext dbContext, ILogger<Fix606Process> logger, TerminationProcess terminationProcess)
    {
        _dbContext = dbContext;
        _logger = logger;
        _terminationProcess = terminationProcess;
        _sematiServiceCallLogCutOffId = configuration.GetValue<int>("SematiServiceCallLogCutOffId");
    }

    public async Task Process(int sematiNotificationActionId)
    {
        using var _ = LogContext.PushProperty("ProcessName", "Fix606");
        using var __ = LogContext.PushProperty("ActionId", sematiNotificationActionId);

        _logger.LogInformation("Processing action {ActionId}", sematiNotificationActionId);

        var action = await _dbContext.SematiNotificationActions.AsNoTracking()
                                                               .Include(x => x.SematiNotification)
                                                               .AsNoTracking()
                                                               .FirstOrDefaultAsync(x => x.Id == sematiNotificationActionId);
                                                               
        if (action?.SematiUpdateTcn is null)
        {
            _logger.LogWarning("Action {ActionId} not found or has no TCN — skipping", sematiNotificationActionId);
            return;
        }

        var personId = action.SematiNotification.IdNumber;
        if (personId is null)
        {
            _logger.LogWarning("No PersonId found for action {ActionId} — skipping", sematiNotificationActionId);
            return;
        }

        using var ___ = LogContext.PushProperty("PersonId", personId);

        var callLog = await _dbContext.SematiServiceCallLogs.AsNoTracking()
                                                            .Where(x => x.Id > _sematiServiceCallLogCutOffId && x.TCN == action.SematiUpdateTcn && x.Operation == "UpdateSematiNotification" && x.RequestText.Contains(action.SematiNotification.IdNumber) && x.Code == 606)
                                                            .OrderByDescending(x => x.Id)
                                                            .FirstOrDefaultAsync();
                                                            
        if (callLog is null || callLog.Code != 606)
        {
            _logger.LogWarning("No 606 service call log found for action {ActionId} (TCN={Tcn}) — skipping", sematiNotificationActionId, action.SematiUpdateTcn);
            return;
        }

        var pendingNumbers = JsonSerializer.Deserialize<SematiServiceResponse>(callLog.ResponseText)?.PendingNumbers;
        if (pendingNumbers is null)
        {
            _logger.LogWarning("Could not deserialize pending numbers for action {ActionId} — skipping", sematiNotificationActionId);
            return;
        }

        _logger.LogInformation("Found {Count} pending numbers for action {ActionId}: {@Numbers}", pendingNumbers.Count, sematiNotificationActionId, pendingNumbers);

        foreach (var number in pendingNumbers)
            await _terminationProcess.TerminateAndSave(number, personId, sematiNotificationActionId);
    }
}
