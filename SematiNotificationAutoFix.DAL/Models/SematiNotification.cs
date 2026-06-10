using System;
using System.Collections.Generic;

namespace SematiNotificationAutoFix.DAL.Models;

public partial class SematiNotification
{
    public int Id { get; set; }

    public string IdNumber { get; set; } = null!;

    public int NotificationCode { get; set; }

    public Guid PushTCN { get; set; }

    public int Status { get; set; }

    public DateTime ReceivedDateTime { get; set; }

    public string? CancelationReason { get; set; }

    public Guid? OperatorTCN { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? MSISDN { get; set; }

    public virtual ICollection<SematiNotificationAction> SematiNotificationActions { get; set; } = new List<SematiNotificationAction>();
}
