using System;
using System.Collections.Generic;

namespace SematiNotificationAutoFix.DAL.Models;

public partial class SematiCallReport
{
    public int SematiCallLogID { get; set; }

    public int SourceID { get; set; }

    public string? personId { get; set; }

    public string? oldOwnerId { get; set; }

    public string? isDefault { get; set; }

    public int? subscriptionType { get; set; }

    public string? msisdn { get; set; }

    public string? msisdnType { get; set; }

    public string? PrimaryICCD { get; set; }

    public string? SecondaryICCD { get; set; }

    public string? PrimaryIMSI { get; set; }

    public string? SecondaryIMSI { get; set; }

    public string? employeeUsername { get; set; }

    public string? employeeId { get; set; }

    public string? deviceId { get; set; }

    public string? operatorTCN { get; set; }

    public int? employeeIdType { get; set; }

    public int? sourceType { get; set; }

    public string? branchAddress { get; set; }

    public string? region { get; set; }

    public int? requestType { get; set; }

    public string? apiKey { get; set; }

    public string? DealerCode { get; set; }

    public string? Channel { get; set; }

    public string? tcn { get; set; }

    public string? code { get; set; }

    public string? message { get; set; }

    public int? NFIQValue { get; set; }

    public DateTime? TimeStamp { get; set; }

    public string? IdType { get; set; }

    public int? OperationID { get; set; }

    public int? exceptionFlag { get; set; }

    public bool? eSim { get; set; }

    public string? orgId { get; set; }

    public int? SetaLogsId { get; set; }
}
