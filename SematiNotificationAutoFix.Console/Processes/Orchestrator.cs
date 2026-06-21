using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Utils;

namespace SematiNotificationAutoFix.Console.Processes;

public class Orchestrator
{
    private readonly Fix606Process _fix606Process;
    private readonly MissingSematiTermination _missingSematiTermination;
    private readonly FixNoActivationActionProcess _fixNoActivationActionProcess;
    private readonly ResubmissionProcess _resubmissionProcess;
    private readonly SqlAgentJobRunner _sqlAgentJobRunner;
    private readonly ILogger<Orchestrator> _logger;
    private readonly string _fix606IdsFilePath = "Data/Fix606.txt";
    private readonly string _missingSematiIdsFilePath = "Data/MissingSematiTermination.txt";
    private readonly string _resubmissionIdsFilePath = "Data/Resubmission.txt";
    private readonly string _fixNoActivationActionIdsFilePath = "Data/FixNoActivationAction.txt";
    private readonly bool _resubmitSucessededProcess;
    private readonly bool _runExtractSematiCallReportJob;

    public Orchestrator(Fix606Process fix606Process,
                        MissingSematiTermination missingSematiTermination,
                        ResubmissionProcess resubmissionProcess,
                        SqlAgentJobRunner sqlAgentJobRunner,
                        FixNoActivationActionProcess fixNoActivationActionProcess,
                        ILogger<Orchestrator> logger,
                        IConfiguration configuration)
    {
        _fix606Process = fix606Process;
        _missingSematiTermination = missingSematiTermination;
        _resubmissionProcess = resubmissionProcess;
        _sqlAgentJobRunner = sqlAgentJobRunner;
        _fixNoActivationActionProcess = fixNoActivationActionProcess;
        _logger = logger;
        _resubmitSucessededProcess = configuration.GetValue("ProcessOptions:ResubmitSucceededProcess", false);
        _runExtractSematiCallReportJob = configuration.GetValue("ProcessOptions:RunExtractSematiCallReportJob", true);
    }

    public async Task RunAsync()
    {
        var fix606Ids = ReadIds(_fix606IdsFilePath);
        var sucessededFix606Ids = await _fix606Process.ProcessAsync(fix606Ids);

        var missingSematiIds = ReadIds(_missingSematiIdsFilePath);
        var sucessededMissingSematiIds = await _missingSematiTermination.ProcessAsync(missingSematiIds);

        var fixNoActivationActionIds = ReadIds(_fixNoActivationActionIdsFilePath);
        var terminatedNoActivationIds = await _fixNoActivationActionProcess.TerminateAsync(fixNoActivationActionIds);

        var needToExtractSematiCallReport = sucessededFix606Ids.Count != 0 || sucessededMissingSematiIds.Count != 0 || terminatedNoActivationIds.Count != 0;
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
            if (_resubmitSucessededProcess)
            {
                actionIdsNeedResubmission.AddRange(sucessededFix606Ids);
                actionIdsNeedResubmission.AddRange(sucessededMissingSematiIds);
                actionIdsNeedResubmission = actionIdsNeedResubmission.Distinct().ToList();
            }

            await _resubmissionProcess.ResubmitAsync(actionIdsNeedResubmission);
        }
        catch (Exception ex) 
        { 
            _logger.LogError(ex, "Unhandled exception during resubmission"); 
        }

        try
        {
            await _fixNoActivationActionProcess.SaveActionsAsync(terminatedNoActivationIds);
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
