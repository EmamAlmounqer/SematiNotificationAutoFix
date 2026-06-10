using System;
using System.Collections.Generic;

namespace SematiNotificationAutoFix.DAL.Models;

public partial class SematiServiceCallLog
{
    public int Id { get; set; }

    public string Url { get; set; } = null!;

    public string Operation { get; set; } = null!;

    public string RequestType { get; set; } = null!;

    public string RequestText { get; set; } = null!;

    public string ResponseText { get; set; } = null!;

    public int Code { get; set; }

    public string? TCN { get; set; }

    public DateTime Timestamp { get; set; }

    public string? DealerCode { get; set; }

    public string? Channel { get; set; }

    public int? NFIQValue { get; set; }

    public int? CheckEligibilityServiceReferenceId { get; set; }

    public string? IAmAppToken { get; set; }

    public int? SetaLogsId { get; set; }
}
