using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Markup;

namespace SematiNotificationAutoFix.Console.Processes;

public class Fix606Process
{
    private readonly ActivationDbContext _dbContext;

    public Fix606Process(ActivationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task Process(int id)
    {
        var sematiNotificationAction = await _dbContext.SematiNotificationActions.FirstOrDefaultAsync(x => x.Id == id);
        if (sematiNotificationAction?.SematiUpdateTcn is null) return;

        var sematiServiceCallLogs = await _dbContext.SematiServiceCallLogs.FirstOrDefaultAsync(x => x.Id > 8900366 && x.TCN == sematiNotificationAction.SematiUpdateTcn);
        if (sematiServiceCallLogs is null || sematiServiceCallLogs.Code != 606) return;

        var pendingNumbers = JsonSerializer.Deserialize<SematiServiceResponse>(sematiServiceCallLogs.ResponseText)?.PendingNumbers;
        var personId = JsonSerializer.Deserialize<SematiServiceRequest>(sematiServiceCallLogs.RequestText)?.PersonId;

        if (pendingNumbers is null || personId is null) return;

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
        } 

        await _dbContext.SematiTerminateNumbers.AddRangeAsync(numberToBeTerminated);
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