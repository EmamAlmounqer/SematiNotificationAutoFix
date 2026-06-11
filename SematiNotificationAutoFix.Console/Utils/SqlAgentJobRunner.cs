using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace SematiNotificationAutoFix.Console.Utils;

public sealed class SqlAgentJobRunner
{
    private readonly string _connectionString;

    public SqlAgentJobRunner(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Default")!;
    }

    public async Task<JobOutcome> RunJobAndWaitAsync(
        string jobName,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        CancellationToken ct = default)
    {
        pollInterval ??= TimeSpan.FromSeconds(15);

        await using var conn = new SqlConnection(_connectionString);
        await conn.OpenAsync(ct);

        // 1. Start the job (returns immediately)
        await using (var start = new SqlCommand("msdb.dbo.sp_start_job", conn))
        {
            start.CommandType = System.Data.CommandType.StoredProcedure;
            start.Parameters.AddWithValue("@job_name", jobName);
            await start.ExecuteNonQueryAsync(ct);
        }

        var startedAtUtc = DateTime.UtcNow;

        // 2. Poll until finished or timeout
        const string pollSql = @"
SELECT TOP (1)
    CASE WHEN ja.start_execution_date IS NOT NULL
          AND ja.stop_execution_date IS NULL THEN 1 ELSE 0 END AS IsRunning,
    jh.run_status   -- 0=Failed 1=Succeeded 2=Retry 3=Canceled, NULL if no history yet
FROM msdb.dbo.sysjobs j
JOIN msdb.dbo.sysjobactivity ja ON ja.job_id = j.job_id
LEFT JOIN msdb.dbo.sysjobhistory jh
       ON jh.job_id = j.job_id AND jh.instance_id = ja.job_history_id
WHERE j.name = @jobName
  AND ja.session_id = (SELECT MAX(session_id) FROM msdb.dbo.syssessions)
ORDER BY ja.run_requested_date DESC;";

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            if (DateTime.UtcNow - startedAtUtc > timeout)
                throw new TimeoutException($"Job '{jobName}' did not finish within {timeout}.");

            await Task.Delay(pollInterval.Value, ct);

            await using var poll = new SqlCommand(pollSql, conn);
            poll.Parameters.AddWithValue("@jobName", jobName);

            await using var reader = await poll.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                continue; // activity row not visible yet, keep waiting

            bool isRunning = reader.GetInt32(0) == 1;
            int? runStatus = reader.IsDBNull(1) ? null : reader.GetInt32(1);

            if (isRunning) continue;

            // stopped — interpret outcome
            return runStatus switch
            {
                1 => JobOutcome.Succeeded,
                0 => JobOutcome.Failed,
                3 => JobOutcome.Canceled,
                2 => JobOutcome.Retry,
                _ => JobOutcome.Unknown
            };
        }
    }
}

public enum JobOutcome { Succeeded, Failed, Canceled, Retry, Unknown }