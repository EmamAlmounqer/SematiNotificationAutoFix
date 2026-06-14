using System.Text.Json.Serialization;

namespace SematiNotificationAutoFix.Console.Models;

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

public class SematiRequest
{
    [JsonPropertyName("person")]
    public PersonInfo? Person { get; set; }
    [JsonPropertyName("mobileNumber")]
    public MobileNumberInfo? MobileNumber { get; set; }
    [JsonPropertyName("operator")]
    public OperatorInfo? Operator { get; set; }
    [JsonPropertyName("requestType")]
    public int RequestType { get; set; }
    [JsonPropertyName("apiKey")]
    public string? ApiKey { get; set; }
    [JsonPropertyName("DealerCode")]
    public string DealerCode { get; set; } = "System";
    [JsonPropertyName("Channel")]
    public string Channel { get; set; } = "DST";
}

public class PersonInfo
{
    [JsonPropertyName("personId")]
    public string? PersonId { get; set; }
    [JsonPropertyName("IdType")]
    public int IdType { get; set; }
    [JsonPropertyName("nationality")]
    public int Nationality { get; set; }
    [JsonPropertyName("fingerIndex")]
    public int FingerIndex { get; set; } = 0;
    [JsonPropertyName("fingerImage")]
    public string FingerImage { get; set; } = "";
    [JsonPropertyName("exceptionFlag")]
    public int ExceptionFlag { get; set; } = 0;
}

public class MobileNumberInfo
{
    [JsonPropertyName("msisdn")]
    public string? Msisdn { get; set; }
    [JsonPropertyName("simList")]
    public List<SimInfo>? SimList { get; set; }
    [JsonPropertyName("subscriptionType")]
    public int SubscriptionType { get; set; } = 0;
    [JsonPropertyName("isDefault")]
    public bool IsDefault { get; set; } = false;
    [JsonPropertyName("msisdnType")]
    public string? MsisdnType { get; set; }
    [JsonPropertyName("oldOwnerId")]
    public string OldOwnerId { get; set; } = "";
}

public class SimInfo
{
    [JsonPropertyName("iccid")]
    public string? Iccid { get; set; }
    [JsonPropertyName("imsi")]
    public string? Imsi { get; set; }
}

public class OperatorInfo
{
    [JsonPropertyName("sourceId")]
    public string SourceId { get; set; } = "7001790299";
    [JsonPropertyName("employeeUsername")]
    public string EmployeeUsername { get; set; } = "System";
    [JsonPropertyName("employeeId")]
    public string EmployeeId { get; set; } = "1109272730";
    [JsonPropertyName("deviceId")]
    public string? DeviceId { get; set; }
    [JsonPropertyName("operatorTCN")]
    public string? OperatorTCN { get; set; }
    [JsonPropertyName("employeeIdType")]
    public int EmployeeIdType { get; set; } = 1;
    [JsonPropertyName("sourceType")]
    public int SourceType { get; set; } = 4;
    [JsonPropertyName("branchAddress")]
    public string BranchAddress { get; set; } = "Automated";
    [JsonPropertyName("region")]
    public string Region { get; set; } = "00";
}

public class SematiServiceResult
{
    public int ResponseCode { get; set; } = -1;
    public NotifyCustomerActionResponse? ObjResponse { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public string Response { get; set; } = string.Empty;
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
