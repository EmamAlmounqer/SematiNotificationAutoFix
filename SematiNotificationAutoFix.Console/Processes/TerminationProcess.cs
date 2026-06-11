using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SematiNotificationAutoFix.Console.Processes;

public class TerminationProcess
{
    private readonly ActivationDbContext _dbContext;
    private readonly ILogger<TerminationProcess> _logger;
    private readonly string _apiKey;
    private readonly string _sematiUrl;
    private static readonly HttpClient _httpClient = new(new HttpClientHandler
    {
        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
    });
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    public TerminationProcess(IConfiguration configuration, ActivationDbContext context, ILogger<TerminationProcess> logger)
    {
        _dbContext = context;
        _logger = logger;
        _sematiUrl = configuration.GetValue<string>("sematiUrl")!;
        _apiKey = configuration.GetValue<string>("_apiKey")!;
    }

    public void ProcessTermination()
    {
        int pageSize = 4;
        int totalPages = _dbContext.SematiTerminateNumbers.Count(t => !t.SematiCode.HasValue || t.SematiCode == -1) / pageSize + 1;

        _logger.LogInformation("Starting termination — {Total} pages of {PageSize}", totalPages, pageSize);

        for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
        {
            var page = _dbContext.SematiTerminateNumbers
                .Where(t => !t.SematiCode.HasValue || t.SematiCode == -1)
                .OrderBy(t => t.ID)
                .Skip(pageIndex * pageSize)
                .Take(pageSize);

            ProcessTermination(page);
        }

        try
        {
            _dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save changes after processing batch");
        }
    }

    public void ProcessTermination(IEnumerable<SematiTerminateNumber> terminateNumbers)
    {
        if (terminateNumbers == null || !terminateNumbers.Any())
            return;

        foreach (SematiTerminateNumber number in terminateNumbers)
        {
            TerminateNumber(number);
        }

    }

    public void TerminateNumber(SematiTerminateNumber number)
    {
        try
        {
            SematiRequest activationRequest = GetSematiRequest(number);

            var formattedRequest = JsonSerializer.Serialize(activationRequest, _jsonOptions);

            _logger.LogInformation("Calling Semati for number {ID} (MSISDN={MSISDN}, RequestType={RequestType})", number.ID, number.MSISDN, activationRequest.RequestType);

            var result = GetSematiServiceResponse(formattedRequest);
            number.SematiCode = result.ResponseCode;
            number.ExecutionTime = DateTime.Now;
            number.TCN = result.ObjResponse?.Tcn;

            _logger.LogInformation("Semati response for number {ID}: code={Code}, TCN={TCN}", number.ID, result.ResponseCode, number.TCN);

            string requestTypeText = number.ProcessId == (int)SematiProcess.Termination ? RequestType.TerminateActivation.ToString() : RequestType.CancelSIM.ToString();
            AddToSematiServiceLog(formattedRequest, result.ObjResponse, "NotifyCustomerAction", requestTypeText, string.IsNullOrWhiteSpace(result.ErrorMessage) ? result.Response : result.ErrorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process number {ID} (MSISDN={MSISDN})", number.ID, number.MSISDN);
            number.SematiCode = null;
            number.ExecutionTime = null;
        }
    }



    private SematiServiceResult GetSematiServiceResponse(string request)
    {
        var result = new SematiServiceResult();

        try
        {
            var content = new StringContent(request, Encoding.UTF8, "application/json");
            var httpResponse = _httpClient.PostAsync(_sematiUrl, content).GetAwaiter().GetResult();
            result.Response = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();

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

    private void AddToSematiServiceLog(string requestText, BaseResponse? objResponse, string operation, string requestType, string apiCallResponse)
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
            _dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save service call log (Operation={Operation}, TCN={TCN})", operation, objResponse?.Tcn);
        }
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
                OperatorTCN = number.OperatorTCN
            },
            RequestType = reqType,
            ApiKey = _apiKey
        };
        return activationRequest;
    }
}

public class NotifyCustomerActionResponse : BaseResponse
{
    [JsonPropertyName("person")]
    public PersonResponse? Person { get; set; }
}

public class BaseResponse
{
    [JsonPropertyName("tcn")]
    public string? Tcn { get; set; }
    [JsonPropertyName("code")]
    public int Code { get; set; }
    [JsonPropertyName("message")]
    public string? Message { get; set; }
    internal string? ErrorCode { get; set; }
}

public class PersonResponse
{
    [JsonPropertyName("first")]
    public string? First { get; set; }
    [JsonPropertyName("father")]
    public string? Father { get; set; }
    [JsonPropertyName("grandfather")]
    public string? Grandfather { get; set; }
    [JsonPropertyName("family")]
    public string? Family { get; set; }
    [JsonPropertyName("trFirst")]
    public string? TrFirst { get; set; }
    [JsonPropertyName("trFather")]
    public string? TrFather { get; set; }
    [JsonPropertyName("trGrandfather")]
    public string? TrGrandfather { get; set; }
    [JsonPropertyName("trFamily")]
    public string? TrFamily { get; set; }
    [JsonPropertyName("gender")]
    public string? Gender { get; set; }
    [JsonPropertyName("maritalStatus")]
    public string? MaritalStatus { get; set; }
    [JsonPropertyName("idExpiryDate")]
    public string? IdExpiryDate { get; set; }
    [JsonPropertyName("nationality")]
    public int Nationality { get; set; }
    [JsonPropertyName("birthdate")]
    public string? Birthdate { get; set; }
    [JsonPropertyName("idIssueDate")]
    public string? IdIssueDate { get; set; }
    [JsonPropertyName("occupation")]
    public int Occupation { get; set; }
    [JsonPropertyName("sponsor")]
    public long Sponsor { get; set; }
}

public enum SematiProcess : int
{
    Termination = 1,
    CancelSim = 2,
    ChangeSubscriptionType = 3

}

public class SematiRequest
{
    public PersonInfo? Person { get; set; }
    public MobileNumberInfo? MobileNumber { get; set; }
    public OperatorInfo? Operator { get; set; }
    public int RequestType { get; set; }
    public string? ApiKey { get; set; }
    public string DealerCode { get; set; } = "System";
    public string Channel { get; set; } = "DST";
}

public class PersonInfo
{
    public string? PersonId { get; set; }
    public int IdType { get; set; }
    public int Nationality { get; set; }
    public int FingerIndex { get; set; } = 0;
    public string FingerImage { get; set; } = "";
    public int ExceptionFlag { get; set; } = 0;
}

public class MobileNumberInfo
{
    public string? Msisdn { get; set; }
    public List<SimInfo>? SimList { get; set; }
    public int SubscriptionType { get; set; } = 0;
    public bool IsDefault { get; set; } = false;
    public string? MsisdnType { get; set; }
    public string OldOwnerId { get; set; } = "";
}

public class SimInfo
{
    public string? Iccid { get; set; }
    public string? Imsi { get; set; }
}

public class OperatorInfo
{
    public string SourceId { get; set; } = "7001790299";
    public string EmployeeUsername { get; set; } = "System";
    public string EmployeeId { get; set; } = "1109272730";
    public string? DeviceId { get; set; }
    public string? OperatorTCN { get; set; }
    public int EmployeeIdType { get; set; } = 1;
    public int SourceType { get; set; } = 4;
    public string BranchAddress { get; set; } = "Automated";
    public string Region { get; set; } = "00";
}

public enum RequestType
{
    VerifyCustomerId = -1,
    NewActivation = 1,
    AddSIM = 2,
    ChangeDefaultNumber = 3,
    TerminateActivation = 4,
    CancelSIM = 5,
    ChangeSubscriptionType = 6,
    TransferOwner = 17
}

public class SematiServiceResult
{
    public int ResponseCode { get; set; } = -1;
    public NotifyCustomerActionResponse? ObjResponse { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
}