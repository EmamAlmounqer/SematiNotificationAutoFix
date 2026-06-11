using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Extensions;
using SematiNotificationAutoFix.Console.Processes;
using SematiNotificationAutoFix.Console.Utils;
using SematiNotificationAutoFix.DAL.Data;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.AddSerilogLogging();

builder.Services.AddDbContext<ActivationDbContext>(opts =>
    opts.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sqlOpts => sqlOpts.UseCompatibilityLevel(120)));

builder.Services.AddSingleton<SqlAgentJobRunner>();
builder.Services.AddScoped<TerminationProcess>();
builder.Services.AddScoped<Fix606Process>();

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var fix606Process = host.Services.GetRequiredService<Fix606Process>();
var sqlAgentJobRunner = host.Services.GetRequiredService<SqlAgentJobRunner>();

try
{
    var ids = File.ReadAllLines("SematiNotificationActionIds.txt")
    .Where(line => !string.IsNullOrWhiteSpace(line))
    .Select(l => int.Parse(l.Trim()))
    .ToList();

    foreach (var id in ids)
    {
        await fix606Process.Process(id);
    }

    var outcome = await sqlAgentJobRunner.RunJobAndWaitAsync(
    "ExtractSemtaiCallReport",
    timeout: TimeSpan.FromMinutes(20),
    pollInterval: TimeSpan.FromSeconds(20));
}
catch (Exception ex)
{
    logger.LogError(ex, "Unhandled exception during processing");
}
finally
{
    await Log.CloseAndFlushAsync();
}

