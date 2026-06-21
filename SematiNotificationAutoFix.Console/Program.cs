using LP.PluginHost.Integration;
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

builder.Services.AddSingleton<NobillClientFactory>();
builder.Services.AddSingleton<SqlAgentJobRunner>();
builder.Services.AddScoped<TerminationProcess>();
builder.Services.AddScoped<Fix606Process>();
builder.Services.AddScoped<MissingSematiTermination>();
builder.Services.AddScoped<ResubmissionProcess>();
builder.Services.AddScoped<FixNoNotificationActionProcess>();
builder.Services.AddScoped<Orchestrator>();

using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();
var orchestrator = host.Services.GetRequiredService<Orchestrator>();

try
{
    await orchestrator.RunAsync();
}
catch (Exception ex)
{
    logger.LogError(ex, "Unhandled exception during processing");
}
finally
{
    await Log.CloseAndFlushAsync();
}
