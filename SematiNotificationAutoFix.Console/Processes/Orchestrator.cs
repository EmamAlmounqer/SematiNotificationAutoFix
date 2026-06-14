using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Utils;

namespace SematiNotificationAutoFix.Console.Processes;

public class Orchestrator
{
    private readonly Fix606Process _fix606Process;
    private readonly MissingSematiTermination _missingSematiTermination;
    private readonly ResubmissionProcess _resubmissionProcess;
    private readonly SqlAgentJobRunner _sqlAgentJobRunner;
    private readonly ILogger<Orchestrator> _logger;

    public Orchestrator(Fix606Process fix606Process, MissingSematiTermination missingSematiTermination, ResubmissionProcess resubmissionProcess, SqlAgentJobRunner sqlAgentJobRunner, ILogger<Orchestrator> logger)
    {
        _fix606Process = fix606Process;
        _missingSematiTermination = missingSematiTermination;
        _resubmissionProcess = resubmissionProcess;
        _sqlAgentJobRunner = sqlAgentJobRunner;
        _logger = logger;
    }

    public async Task RunAsync()
    {
        var fix606Ids = ReadIds("Data/Fix606.txt");

        foreach (var id in fix606Ids)
        {
            try { await _fix606Process.Process(id); }
            catch (Exception ex) { _logger.LogError(ex, "Unhandled exception processing action {ActionId}", id); }
        }

        var missingSematiIds = ReadIds("Data/MissingSematiTermination.txt");
        foreach (var id in missingSematiIds)
        {
            try { await _missingSematiTermination.Process(id); }
            catch (Exception ex) { _logger.LogError(ex, "Unhandled exception processing action {ActionId}", id); }
        }

        var outcome = await _sqlAgentJobRunner.RunJobAndWaitAsync(
           "ExtractSematiCallReport",
           timeout: TimeSpan.FromMinutes(20),
           pollInterval: TimeSpan.FromSeconds(20));

        _logger.LogInformation("SQL agent job outcome: {Outcome}", outcome);

        try
        {
            var resubmissionIds = ReadIds("Data/Resubmission.txt");
            var actionIdsNeedResubmission = resubmissionIds.Union(fix606Ids).Union(missingSematiIds).ToList();
            await _resubmissionProcess.ResubmitAsync(actionIdsNeedResubmission);
        }
        catch (Exception ex) { _logger.LogError(ex, "Unhandled exception during resubmission"); }
    }

    private static List<int> ReadIds(string path)
    {
        if (!File.Exists(path)) return [];
        return File.ReadAllLines(path)
            .Select(line => int.TryParse(line.Trim(), out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();
    }
}
