using Microsoft.Extensions.Configuration;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace SematiNotificationAutoFix.Console.Processes;

public class TerminationProcess
{
    private readonly ActivationDbContext _dbContext;
    static string request = "{{\"person\":{{\"personId\":\"{0}\",\"IdType\":{1},\"nationality\":{5},\"fingerIndex\":0,\"fingerImage\":\"\",\"exceptionFlag\":0}},\"mobileNumber\":{{\"msisdn\":\"{2}\",\"simList\":{6},\"subscriptionType\":0,\"isDefault\":false,\"msisdnType\":\"{3}\",\"oldOwnerId\":\"\"}},\"operator\":{{ \"sourceId\":\"7001790299\",\"employeeUsername\":\"System\",\"employeeId\":\"1109272730\",\"deviceId\":null,\"operatorTCN\":\"{4}\",\"employeeIdType\":1,\"sourceType\":4,\"branchAddress\":\"Automated\",\"region\":\"00\"}},\"requestType\":{7},\"apiKey\":\"2047600105301466696j01l8qZq9ttXjv0RylU/wP3mpGU0s0LFE85EYRg+Ouo=\",\"DealerCode\":\"System\",\"Channel\":\"DST\"}}";
    static string simListFormat = "[{\"iccid\":\"{1}\",\"imsi\":\"{0}\"}]";
    private readonly string _sematiUrl;

    public TerminationProcess(IConfiguration configuration, ActivationDbContext context)
    {
        _dbContext = context;
        _sematiUrl = configuration.GetValue<string>("sematiUrl")!;
    }

    public void ProcessTermination()
    {
        int pageSize = 4;
        int totalPages = _dbContext.SematiTerminateNumbers.Count(t => !t.SematiCode.HasValue || t.SematiCode == -1) / pageSize + 1;

        if (totalPages > 0)
        {
            for (int pageIndex = 0; pageIndex < totalPages; pageIndex++)
            {
                IEnumerable<SematiTerminateNumber> numberListPage = _dbContext.SematiTerminateNumbers.Where(t => (!t.SematiCode.HasValue || t.SematiCode == -1)).OrderBy(t => t.ID).Skip(pageIndex * pageSize).Take(pageSize);
                ProcessTermination(numberListPage);
            }
        }
    }

    public void ProcessTermination(IEnumerable<SematiTerminateNumber> numberListPage)
    {
        string formattedRequest = string.Empty;
        NotifyCustomerActionResponse objResponse = null;
        string errorMessage = string.Empty;
        string response = string.Empty;

        if (numberListPage != null && numberListPage.Count() > 0)
        {
            foreach (SematiTerminateNumber number in numberListPage)
            {
                try
                {
                    string requestType = number.ProcessId == (int)SematiProcess.Termination ? ((int)RequestType.TerminateActivation).ToString() : ((int)RequestType.CancelSIM).ToString();
                    number.OperatorTCN = Guid.NewGuid().ToString();

                    string simList = string.IsNullOrWhiteSpace(number.ICCID) ? "null" : string.Format(simListFormat, number.IMSI, number.ICCID);
                    formattedRequest = string.Format(request, number.IDNumber, number.IDTypeID, number.MSISDN, number.SubscriptionType, number.OperatorTCN, number.NationalityID.HasValue ? number.NationalityID : 0, simList, requestType);
                    //lblId.Text = number.ID.ToString();
                    int errorCode = GetSematiServiceResponse(formattedRequest, out objResponse, out errorMessage, out response);
                    number.SematiCode = errorCode;
                    number.ExecutionTime = DateTime.Now;
                    number.TCN = objResponse != null ? objResponse.tcn : null;

                    //if (errorCode == 600)
                    {
                        string requestTypeText = number.ProcessId == (int)SematiProcess.Termination ? RequestType.TerminateActivation.ToString() : RequestType.CancelSIM.ToString();
                        AddToSematiServiceLog(formattedRequest, objResponse, "NotifyCustomerAction", requestTypeText, (string.IsNullOrWhiteSpace(errorMessage) ? response : errorMessage));
                        //context.SaveChanges();
                    }

                    try
                    {
                        //File.AppendAllText(logFile, number.ID + " - Ended " + errorCode + Environment.NewLine + Environment.NewLine);
                    }
                    catch (Exception ex1)
                    {
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        //File.AppendAllText(logFile, number.ID + " " + ex.Message + " " + ex.StackTrace);
                    }
                    catch (Exception ex1)
                    {
                        number.SematiCode = null;
                        number.ExecutionTime = null;
                    }
                }
            }
            try
            {
                _dbContext.SaveChanges();
            }
            catch (Exception ex1)
            {
            }
        }
    }


    private int GetSematiServiceResponse(string request, out NotifyCustomerActionResponse objResponse, out string errorMessage, out string response)
    {
        var httpWebRequest = (HttpWebRequest)WebRequest.Create(_sematiUrl);
        response = string.Empty;
        int responseCode;
        httpWebRequest.ContentType = "application/json";
        httpWebRequest.Method = "POST";
        httpWebRequest.Proxy = null;
        objResponse = null;
        errorMessage = string.Empty;

        try
        {
            System.Net.ServicePointManager.Expect100Continue = false;

            using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
            {
                streamWriter.Write(request);
                streamWriter.Flush();
                streamWriter.Close();
            }

            System.Net.ServicePointManager.ServerCertificateValidationCallback = (senderX, certificate, chain, sslPolicyErrors) => { return true; };
            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                response = streamReader.ReadToEnd();
            }

            httpResponse.Close();

            responseCode = 600;
            if (!string.IsNullOrWhiteSpace(response))
            {
                //objResponse = new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<NotifyCustomerActionResponse>(response);
                responseCode = objResponse.code;
            }
            else
            {
                responseCode = -1;
            }
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            //System.IO.File.AppendAllText(sematiLogFile, "Request: " + request + "  MEssage: " + ex.Message + Environment.NewLine + "Stack Trace: " + ex.StackTrace);
            responseCode = -1;
        }

        return responseCode;
    }

    internal void AddToSematiServiceLog(string requestText, BaseResponse objResponse, string operation, string requestType, string apiCallResponse)
    {
        SematiServiceCallLog objSematiServiceCallLog = null;
        try
        {
            string responseText = apiCallResponse;
            objSematiServiceCallLog = new SematiServiceCallLog()
            {
                Code = objResponse.code,
                Operation = operation,
                RequestText = requestText,
                ResponseText = responseText,
                RequestType = requestType,
                TCN = objResponse.tcn,
                Url = _sematiUrl,
                Timestamp = DateTime.Now,
                DealerCode = "System",
                Channel = "TerminationTool"
            };

             _dbContext.SematiServiceCallLogs.Add(objSematiServiceCallLog);
            _dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            string logContent = string.Format("Request:{1}{0}{1}{1}Response:{1}{2}{1}{1}Error Message:{3}", requestText, Environment.NewLine, objResponse, ex.Message);
            //try
            //{
            //    if (!File.Exists(sematiLogFile))
            //    {
            //        File.Create(sematiLogFile);
            //        File.AppendAllText(sematiLogFile, logContent);
            //    }
            //}
            //catch
            //{
            //    File.AppendAllText(logFile, logContent);
            //    // Do Nothing.
            //}

        }
    }
}


public class NotifyCustomerActionResponse : BaseResponse
{
    public PersonResponse person { get; set; }
}

public class BaseResponse
{
    public string tcn { get; set; }
    public int code { get; set; }
    public string message { get; set; }
    internal string ErrorCode { get; set; }
}

[DataContract]
public class PersonResponse
{
    public string first { get; set; }
    public string father { get; set; }
    public string grandfather { get; set; }
    public string family { get; set; }
    public string trFirst { get; set; }
    public string trFather { get; set; }
    public string trGrandfather { get; set; }
    public string trFamily { get; set; }
    public string gender { get; set; }
    public string maritalStatus { get; set; }
    public string idExpiryDate { get; set; }
    public int nationality { get; set; }
    public string birthdate { get; set; }
    public string idIssueDate { get; set; }
    public int occupation { get; set; }
    public long sponsor { get; set; }
}

public enum SematiProcess : int
{
    Termination = 1,
    CancelSim = 2,
    ChangeSubscriptionType = 3

}

public class ActivationRequest
{
    public PersonInfo Person { get; set; }
    public MobileNumberInfo MobileNumber { get; set; }
    public OperatorInfo Operator { get; set; }
    public int RequestType { get; set; }
    public string ApiKey { get; set; }
    public string DealerCode { get; set; } = "System";
    public string Channel { get; set; } = "DST";
}

public class PersonInfo
{
    public string PersonId { get; set; }
    public int IdType { get; set; }
    public int Nationality { get; set; }
    public int FingerIndex { get; set; } = 0;
    public string FingerImage { get; set; } = "";
    public int ExceptionFlag { get; set; } = 0;
}

public class MobileNumberInfo
{
    public string Msisdn { get; set; }
    public List<SimInfo> SimList { get; set; }   // strongly-typed instead of pre-serialized string
    public int SubscriptionType { get; set; } = 0;
    public bool IsDefault { get; set; } = false;
    public string MsisdnType { get; set; }
    public string OldOwnerId { get; set; } = "";
}

public class SimInfo
{
    public string Iccid { get; set; }
    public string Imsi { get; set; }
}

public class OperatorInfo
{
    public string SourceId { get; set; } = "7001790299";
    public string EmployeeUsername { get; set; } = "System";
    public string EmployeeId { get; set; } = "1109272730";
    public string DeviceId { get; set; }
    public string OperatorTCN { get; set; }
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