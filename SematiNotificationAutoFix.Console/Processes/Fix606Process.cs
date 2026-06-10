using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Serilog.Context;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SematiNotificationAutoFix.Console.Processes;

public class Fix606Process
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<Fix606Process> _logger;
    private readonly int _sematiServiceCallLogCutOffId;

    public Fix606Process(IConfiguration configuration, ActivationDbContext dbContext, ILogger<Fix606Process> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _sematiServiceCallLogCutOffId = configuration.GetValue<int>("SematiServiceCallLogCutOffId");
    }

    public async Task Process(int sematiNotificationActionId)
    {
        using var _ = LogContext.PushProperty("ProcessName", "Fix606");
        using var __ = LogContext.PushProperty("ActionId", sematiNotificationActionId);

        _logger.LogInformation("Processing action {ActionId}", sematiNotificationActionId);

        var sematiNotificationAction = await _dbContext.SematiNotificationActions.FirstOrDefaultAsync(x => x.Id == sematiNotificationActionId);
        if (sematiNotificationAction?.SematiUpdateTcn is null)
        {
            _logger.LogWarning("Action {ActionId} not found or has no TCN — skipping", sematiNotificationActionId);
            return;
        }

        using var ___ = LogContext.PushProperty("Tcn", sematiNotificationAction.SematiUpdateTcn);

        var sematiServiceCallLogs = await _dbContext.SematiServiceCallLogs.FirstOrDefaultAsync(x => x.Id > _sematiServiceCallLogCutOffId && x.TCN == sematiNotificationAction.SematiUpdateTcn);
        if (sematiServiceCallLogs is null || sematiServiceCallLogs.Code != 606)
        {
            _logger.LogWarning("No 606 service call log found for action {ActionId} (TCN={Tcn}) — skipping", sematiNotificationActionId, sematiNotificationAction.SematiUpdateTcn);
            return;
        }

        var pendingNumbers = JsonSerializer.Deserialize<SematiServiceResponse>(sematiServiceCallLogs.ResponseText)?.PendingNumbers;
        var personId = JsonSerializer.Deserialize<SematiServiceRequest>(sematiServiceCallLogs.RequestText)?.PersonId;

        if (pendingNumbers is null || personId is null)
        {
            _logger.LogWarning("Could not deserialize pending numbers or person ID for action {ActionId} — skipping", sematiNotificationActionId);
            return;
        }

        using var ____ = LogContext.PushProperty("PersonId", personId);

        List<SematiTerminateNumber> numberToBeTerminated = [];

        foreach (var number in pendingNumbers)
        {
            numberToBeTerminated.Add(new SematiTerminateNumber
            {
                MSISDN = number,
                IDNumber = personId,
                IDTypeID = GetIdTypeID(personId),
                SematiCode = -1,
                NationalityID = 113,
                SubscriptionType = "V"
            });

            _logger.LogInformation("Add {MSISDN} terminate numbers for action {ActionId} (PersonId={PersonId})", number, sematiNotificationActionId, personId);
        }

        await _dbContext.SematiTerminateNumbers.AddRangeAsync(numberToBeTerminated);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Inserted {Count} terminate numbers for action {ActionId} (PersonId={PersonId})", numberToBeTerminated.Count, sematiNotificationActionId, personId);
    }

    private byte GetIdTypeID(string s)
    {
        if (string.IsNullOrEmpty(s)) return 3;
        var a = s[0];
        if (a == '1') return 1;
        if (a == '2') return 2;
        return 3;
    }
}


public class SematiServiceResponse
{
    [JsonPropertyName("tcn")]
    public string Tcn { get; set; } = default!;

    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; } = default!;

    [JsonPropertyName("pendingNumbers")]
    public List<string> PendingNumbers { get; set; } = new();
}

public class SematiServiceRequest
{
    [JsonPropertyName("apiKey")]
    public string ApiKey { get; set; } = default!;

    [JsonPropertyName("operatorTCN")]
    public string OperatorTcn { get; set; } = default!;

    [JsonPropertyName("personId")]
    public string PersonId { get; set; } = default!;

    [JsonPropertyName("notificationCode")]
    public int NotificationCode { get; set; }

    [JsonPropertyName("status")]
    public int Status { get; set; }

    [JsonPropertyName("tccTCN")]
    public string TccTcn { get; set; } = default!;

    [JsonPropertyName("update_status")]
    public int UpdateStatus { get; set; }
}