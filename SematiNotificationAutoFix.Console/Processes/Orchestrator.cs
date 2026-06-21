using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Utils;

namespace SematiNotificationAutoFix.Console.Processes;

public class Orchestrator
{
    private readonly Fix606Process _fix606Process;
    private readonly MissingSematiTermination _missingSematiTermination;
    private readonly FixNoNotificationActionProcess _fixNoNotificationActionProcess;
    private readonly ResubmissionProcess _resubmissionProcess;
    private readonly SqlAgentJobRunner _sqlAgentJobRunner;
    private readonly ILogger<Orchestrator> _logger;
    private readonly string _fix606IdsFilePath = "Data/Fix606.txt";
    private readonly string _missingSematiIdsFilePath = "Data/MissingSematiTermination.txt";
    private readonly string _resubmissionIdsFilePath = "Data/Resubmission.txt";
    private readonly string _fixNoNotificationActionIdsFilePath = "Data/FixNoNotificationAction.txt";
    private readonly bool _resubmitSucceededProcess;
    private readonly bool _runExtractSematiCallReportJob;

    public Orchestrator(Fix606Process fix606Process,
                        MissingSematiTermination missingSematiTermination,
                        ResubmissionProcess resubmissionProcess,
                        SqlAgentJobRunner sqlAgentJobRunner,
                        FixNoNotificationActionProcess fixNoNotificationActionProcess,
                        ILogger<Orchestrator> logger,
                        IConfiguration configuration)
    {
        _fix606Process = fix606Process;
        _missingSematiTermination = missingSematiTermination;
        _resubmissionProcess = resubmissionProcess;
        _sqlAgentJobRunner = sqlAgentJobRunner;
        _fixNoNotificationActionProcess = fixNoNotificationActionProcess;
        _logger = logger;
        _resubmitSucceededProcess = configuration.GetValue("ProcessOptions:ResubmitSucceededProcess", false);
        _runExtractSematiCallReportJob = configuration.GetValue("ProcessOptions:RunExtractSematiCallReportJob", true);
    }

    public async Task RunAsync()
    {
        var fix606ActionIds = ReadIds(_fix606IdsFilePath);
        var succeededFix606ActionIds = await _fix606Process.ProcessAsync(fix606ActionIds);

        var missingSematiActionIds = ReadIds(_missingSematiIdsFilePath);
        var succeededMissingSematiActionIds = await _missingSematiTermination.ProcessAsync(missingSematiActionIds);

        var fixNoNotificationActionNotificationIds = ReadIds(_fixNoNotificationActionIdsFilePath);
        var terminatedNoNotificationActionIds = await _fixNoNotificationActionProcess.TerminateAsync(fixNoNotificationActionNotificationIds);

        var needToExtractSematiCallReport = succeededFix606ActionIds.Count != 0 || succeededMissingSematiActionIds.Count != 0 || terminatedNoNotificationActionIds.Count != 0;
        if (_runExtractSematiCallReportJob && needToExtractSematiCallReport)
        {
            await _sqlAgentJobRunner.RunJobAndWaitAsync(
              "ExtractSematiCallReport",
              timeout: TimeSpan.FromMinutes(20),
              pollInterval: TimeSpan.FromSeconds(20));
        }

        try
        {
            var resubmissionIds = ReadIds(_resubmissionIdsFilePath);
            var actionIdsNeedResubmission = resubmissionIds.ToList();
            if (_resubmitSucceededProcess)
            {
                actionIdsNeedResubmission.AddRange(succeededFix606ActionIds);
                actionIdsNeedResubmission.AddRange(succeededMissingSematiActionIds);
                actionIdsNeedResubmission.AddRange(terminatedNoNotificationActionIds);
                actionIdsNeedResubmission = actionIdsNeedResubmission.Distinct().ToList();
            }

            await _resubmissionProcess.ResubmitAsync(actionIdsNeedResubmission);
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "Unhandled exception during resubmission"); 
        }

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
