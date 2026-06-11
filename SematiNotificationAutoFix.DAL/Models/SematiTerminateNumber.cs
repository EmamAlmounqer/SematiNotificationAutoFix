using System;
using System.Collections.Generic;

namespace SematiNotificationAutoFix.DAL.Models;

public partial class SematiTerminateNumber
{
    public int ID { get; set; }

    public string MSISDN { get; set; } = null!;

    public string? IMSI { get; set; }

    public string? ICCID { get; set; }

    public string SubscriptionType { get; set; } = null!;

    public string IDNumber { get; set; } = null!;

    public byte? IDTypeID { get; set; }

    public int? SematiCode { get; set; }

    public DateTime? ExecutionTime { get; set; }

    public string? TCN { get; set; }

    public string? OperatorTCN { get; set; }

    public int? NationalityID { get; set; }

    public bool? IsProcessed { get; set; }

    public bool? IsTerminatedByMistake { get; set; }

    public int ProcessId { get; set; } = 1;

    public bool? AsPerNICRequest { get; set; }

    public bool? IsVirginCleanup20171105 { get; set; }

    public bool? Virgin_0_In_quarantine_20170419 { get; set; }

    public bool? IsVirgin_20170516 { get; set; }

    public bool? IsFriendi20170517 { get; set; }

    public bool? IsFriendi20170518 { get; set; }

    public bool? IsRetryRequired { get; set; }

    public byte? CorrectedIDTypeID { get; set; }

    public bool? IsForSimSwap { get; set; }

    public bool? IsToBeDeleted { get; set; }

    public bool? IsIDNumberNotFount { get; set; }
}
