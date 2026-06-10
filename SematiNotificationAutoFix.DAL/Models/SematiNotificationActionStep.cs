using System;
using System.Collections.Generic;

namespace SematiNotificationAutoFix.DAL.Models;

public partial class SematiNotificationActionStep
{
    public int Id { get; set; }

    public string Action { get; set; } = null!;

    public string? EnglishMessageTemplate { get; set; }

    public string? ArabicMessageTemplate { get; set; }

    public int ActionType { get; set; }

    public virtual ICollection<SematiNotificationAction> SematiNotificationActions { get; set; } = new List<SematiNotificationAction>();
}
