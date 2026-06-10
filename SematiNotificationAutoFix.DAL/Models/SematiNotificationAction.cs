using System;
using System.Collections.Generic;

namespace SematiNotificationAutoFix.DAL.Models;

public partial class SematiNotificationAction
{
    public int Id { get; set; }

    public string MSISDN { get; set; } = null!;

    public DateTime ExpectedActionDate { get; set; }

    public string? SematiActionCode { get; set; }

    public string? SematiActionTcn { get; set; }

    public DateTime? ExecutedAt { get; set; }

    public bool? IsTcnUpdated { get; set; }

    public string? SematiUpdateCode { get; set; }

    public string? SematiUpdateTcn { get; set; }

    public string? CancelationReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public int Status { get; set; }

    public int SematiNotificationId { get; set; }

    public DateTime? SematiNotifiedAt { get; set; }

    public int SematiNotificationActionStepId { get; set; }

    public string? SematiErrorMessage { get; set; }

    public string? AccountNumber { get; set; }

    public int? RetriesCount { get; set; }

    public DateTime? LastUpdated { get; set; }

    public virtual SematiNotification SematiNotification { get; set; } = null!;

    public virtual SematiNotificationActionStep SematiNotificationActionStep { get; set; } = null!;
}
