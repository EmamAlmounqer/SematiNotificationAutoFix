using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SematiNotificationAutoFix.Console.Extensions;
using SematiNotificationAutoFix.Console.Processes;
using SematiNotificationAutoFix.DAL.Data;

var builder = Host.CreateApplicationBuilder(args);

builder.AddSerilogLogging();

builder.Services.AddDbContext<ActivationDbContext>(opts =>
    opts.UseSqlServer(
        builder.Configuration.GetConnectionString("Default"),
        sqlOpts => sqlOpts.UseCompatibilityLevel(120)));

builder.Services.AddScoped<Fix606Process>();

using var host = builder.Build();

var ids = File.ReadAllLines("SematiNotificationActionIds.txt")
    .Where(line => !string.IsNullOrWhiteSpace(line))
    .Select(l => int.Parse(l.Trim()))
    .ToList();

var fix606Process = host.Services.GetRequiredService<Fix606Process>();
foreach (var id in ids)
{
    await fix606Process.Process(id);
}

