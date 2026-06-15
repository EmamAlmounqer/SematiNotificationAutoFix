using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.Console.Enums;
using SematiNotificationAutoFix.Console.Models;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using Serilog.Context;
using System.Text;
using System.Text.Json;

namespace SematiNotificationAutoFix.Console.Processes;

public class TerminationProcess
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<TerminationProcess> _logger;
    private readonly string _apiKey;
    private readonly string _sematiUrl;
    private readonly string _sourceId;
    private readonly string _employeeId;
    private readonly string[] _allowedTerminationCodes = [ "600", "780" ];

    private static readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    });
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    public TerminationProcess(IConfiguration configuration, ActivationDbContext context, ILogger<TerminationProcess> logger)
    {
        _dbContext = context;
        _logger = logger;
        _sematiUrl = configuration.GetValue<string>("Semati:Url")!;
        _apiKey = configuration.GetValue<string>("Semati:ApiKey")!;
        _sourceId = configuration.GetValue<string>("Semati:SourceId")!;
        _employeeId = configuration.GetValue<string>("Semati:EmployeeId")!;
    }

    public async Task<SematiServiceResult> TerminateNumberAsync(SematiTerminateNumber number)
    {
        try
        {
            SematiRequest activationRequest = GetSematiRequest(number);
            using var _ = LogContext.PushProperty("OperatorTCN", activationRequest?.Operator?.OperatorTCN);

            var formattedRequest = JsonSerializer.Serialize(activationRequest, _jsonOptions);

            _logger.LogInformation("Calling Semati (MSISDN={MSISDN}, PersonId={PersonId}, RequestType={RequestType})", number.MSISDN, number.IDNumber, activationRequest?.RequestType);

            var result = await GetSematiServiceResponseAsync(formattedRequest);
            number.SematiCode = result.ResponseCode;
            number.ExecutionTime = DateTime.Now;
            number.TCN = result.ObjResponse?.Tcn;

            _logger.LogInformation("Semati response: code={Code}, TCN={TCN}", result.ResponseCode, number.TCN);

            string requestTypeText = number.ProcessId == (int)SematiProcess.Termination ? RequestType.TerminateActivation.ToString() : RequestType.CancelSIM.ToString();
            await AddToSematiServiceLogAsync(formattedRequest, result.ObjResponse, "NotifyCustomerAction", requestTypeText, string.IsNullOrWhiteSpace(result.ErrorMessage) ? result.Response : result.ErrorMessage);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process number (MSISDN={MSISDN}, PersonId={PersonId})", number.MSISDN, number.IDNumber);
            number.SematiCode = null;
            number.ExecutionTime = null;
            return new SematiServiceResult { ResponseCode = -1, ErrorMessage = ex.Message };
        }
    }

    private async Task<SematiServiceResult> GetSematiServiceResponseAsync(string request)
    {
        var result = new SematiServiceResult();

        try
        {
            var content = new StringContent(request, Encoding.UTF8, "application/json");
            var httpResponse = await _httpClient.PostAsync(_sematiUrl, content);
            result.Response = await httpResponse.Content.ReadAsStringAsync();

            if (!string.IsNullOrWhiteSpace(result.Response))
            {
                result.ObjResponse = JsonSerializer.Deserialize<NotifyCustomerActionResponse>(result.Response, _jsonOptions);
                result.ResponseCode = result.ObjResponse?.Code ?? -1;
            }
            else
            {
                _logger.LogWarning("Empty response from Semati API");
                result.ResponseCode = -1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "HTTP call to Semati API failed");
            result.ErrorMessage = ex.Message;
            result.ResponseCode = -1;
        }

        return result;
    }

    private async Task AddToSematiServiceLogAsync(string requestText, BaseResponse? objResponse, string operation, string requestType, string apiCallResponse)
    {
        try
        {
            _dbContext.SematiServiceCallLogs.Add(new SematiServiceCallLog
            {
                Code = objResponse?.Code ?? -1,
                Operation = operation,
                RequestText = requestText,
                ResponseText = apiCallResponse,
                RequestType = requestType,
                TCN = objResponse?.Tcn,
                Url = _sematiUrl,
                Timestamp = DateTime.Now,
                DealerCode = "System",
                Channel = "TerminationTool"
            });
            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save service call log (Operation={Operation}, TCN={TCN})", operation, objResponse?.Tcn);
        }
    }

    public async Task<bool> TerminateAndSaveAsync(string msisdn, string personId, int actionId)
    {
        _logger.LogInformation("Terminating number {MSISDN} for action {ActionId} (PersonId={PersonId})", msisdn, actionId, personId);

        var terminateNumber = new SematiTerminateNumber
        {
            MSISDN = msisdn,
            IDNumber = personId,
            IDTypeID = GetIdTypeByPersonId(personId),
            SematiCode = -2,
            NationalityID = 113,
            SubscriptionType = "V"
        };

        var result = await TerminateNumberAsync(terminateNumber);
        if (!_allowedTerminationCodes.Contains(result.ResponseCode.ToString()))
        {
            _logger.LogError("Failed to terminate number {MSISDN} for action {ActionId} (PersonId={PersonId}): {ErrorMessage}", msisdn, actionId, personId, result.ErrorMessage);
            return false;
        }

        await _dbContext.SematiTerminateNumbers.AddAsync(terminateNumber);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Terminated number {MSISDN} — SematiTerminateNumberId={Id}", msisdn, terminateNumber.ID);
        return true;
    }

    public static byte GetIdTypeByPersonId(string s)
    {
        if (string.IsNullOrEmpty(s)) return 3;
        var a = s[0];
        if (a == '1') return 1;
        if (a == '2') return 2;
        return 3;
    }

    private SematiRequest GetSematiRequest(SematiTerminateNumber number)
    {
        int reqType = number.ProcessId == (int)SematiProcess.Termination ? (int)RequestType.TerminateActivation : (int)RequestType.CancelSIM;
        number.OperatorTCN = Guid.NewGuid().ToString();

        var activationRequest = new SematiRequest
        {
            Person = new PersonInfo
            {
                PersonId = number.IDNumber,
                IdType = number.IDTypeID ?? 0,
                Nationality = number.NationalityID ?? 0
            },
            MobileNumber = new MobileNumberInfo
            {
                Msisdn = number.MSISDN,
                MsisdnType = number.SubscriptionType,
                SimList = string.IsNullOrWhiteSpace(number.ICCID) ? null : [new() { Iccid = number.ICCID, Imsi = number.IMSI }]
            },
            Operator = new OperatorInfo
            {
                OperatorTCN = number.OperatorTCN,
                SourceId = _sourceId,
                EmployeeId = _employeeId
            },
            RequestType = reqType,
            ApiKey = _apiKey
        };
        return activationRequest;
    }
}
