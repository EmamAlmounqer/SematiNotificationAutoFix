using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Utils;

namespace SematiNotificationAutoFix.Console.Processes;

public class Fix606ByDBOrchestrator : IOrchestrator
{
    private readonly Fix606Process _fix606Process;
    private readonly ResubmissionProcess _resubmissionProcess;
    private readonly SqlAgentJobRunner _sqlAgentJobRunner;
    private readonly ILogger<UsingTextFileOrchestrator> _logger;
    private readonly bool _resubmitSucceededProcess;
    private readonly bool _runExtractSematiCallReportJob;

    public Fix606ByDBOrchestrator(Fix606Process fix606Process,
                        ResubmissionProcess resubmissionProcess,
                        SqlAgentJobRunner sqlAgentJobRunner,
                        ILogger<UsingTextFileOrchestrator> logger,
                        IConfiguration configuration)
    {
        _fix606Process = fix606Process;
        _resubmissionProcess = resubmissionProcess;
        _sqlAgentJobRunner = sqlAgentJobRunner;
        _logger = logger;
        _resubmitSucceededProcess = configuration.GetValue("ProcessOptions:ResubmitSucceededProcess", false);
        _runExtractSematiCallReportJob = configuration.GetValue("ProcessOptions:RunExtractSematiCallReportJob", true);
    }

    public async Task RunAsync()
    {
        var fix606ActionIds = await _fix606Process.Get606FromDB();
        if (fix606ActionIds == null || fix606ActionIds.Count == 0)
        {
            _logger.LogInformation("No 606 notification Found");
            return;
        }

        _logger.LogInformation("Found {NotificationActionCount} 606 notification, ids: {@fix606ActionIds}", fix606ActionIds.Count, fix606ActionIds);

        var succeededFix606ActionIds = await _fix606Process.ProcessAsync(fix606ActionIds);

        var needToExtractSematiCallReport = succeededFix606ActionIds.Count != 0;
        if (_runExtractSematiCallReportJob && needToExtractSematiCallReport)
        {
            await _sqlAgentJobRunner.RunJobAndWaitAsync(
              "ExtractSematiCallReport",
              timeout: TimeSpan.FromMinutes(20),
              pollInterval: TimeSpan.FromSeconds(20));
        }

        try
        {
            List<int> actionIdsNeedResubmission = [];
            if (_resubmitSucceededProcess)
            {
                actionIdsNeedResubmission.AddRange(succeededFix606ActionIds);
                actionIdsNeedResubmission = actionIdsNeedResubmission.Distinct().ToList();
            }

            await _resubmissionProcess.ResubmitAsync(actionIdsNeedResubmission);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during resubmission");
        }

    }

}
