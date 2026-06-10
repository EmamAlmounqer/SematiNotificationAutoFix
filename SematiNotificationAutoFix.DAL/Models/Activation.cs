using System;
using System.Collections.Generic;

namespace SematiNotificationAutoFix.DAL.Models;

public partial class Activation
{
    public int ID { get; set; }

    public DateTime CreatedOn { get; set; }

    public int SubscriptionTypeId { get; set; }

    public int IdentityMasterId { get; set; }

    public string IMSI { get; set; } = null!;

    public string MSISDN { get; set; } = null!;

    public string? DistributorCode { get; set; }

    public string? DealerCode { get; set; }

    public bool IsActive { get; set; }

    public byte PaperStatusId { get; set; }

    public DateTime? PaperStatusUpdateDate { get; set; }

    public DateTime? ResumissionDate { get; set; }

    public bool IsFirstCharegableEventReceived { get; set; }

    public DateTime? FirstChargeableEventReceivedTime { get; set; }

    public bool? IsPaperCorrect { get; set; }

    public bool? IsCustomerInfoCorrect { get; set; }

    public DateTime? LastUpdateDate { get; set; }

    public string NobillCustomerNo { get; set; } = null!;

    public string NobillAccountNumber { get; set; } = null!;

    public string? FormSerial { get; set; }

    public string? ActivationChannel { get; set; }

    public string? QualityGrade { get; set; }

    public string? RejectionReason { get; set; }

    public byte PhysicalFormStatusId { get; set; }

    public DateTime? PhysicalFormSalesReceivedDate { get; set; }

    public string? PhysicalFormReceivedSalesCode { get; set; }

    public DateTime? PhysicalFormOfficeReceivedDate { get; set; }

    public string? PUK { get; set; }

    public string? ICCID { get; set; }

    public string? Longitude { get; set; }

    public string? Latitude { get; set; }

    public int BrandId { get; set; }

    public string? KitID { get; set; }

    public bool IsPrimary { get; set; }

    public bool IsVanityActivation { get; set; }

    public string? Receipt_Voucher_Number { get; set; }

    public DateTime? FirstEventReceivedDate { get; set; }

    public DateTime? TerminationDate { get; set; }

    public string? Email { get; set; }

    public int? IdentityVerificationStatusId { get; set; }

    public DateTime? IdentityVerificationStatusUpdateDate { get; set; }

    public bool? IsDefault { get; set; }

    public string? CRM_ContactID { get; set; }

    public string? CRM_AccountID { get; set; }

    public bool? IsPortInActivation { get; set; }

    public bool? IsPortedOut { get; set; }

    public bool? IsSelfcareOnboarded { get; set; }

    public DateTime? PortOutDateTime { get; set; }

    public bool? IsEsimActivation { get; set; }

    public string? LocationID { get; set; }

    public bool? IsDealerAppOnboarded { get; set; }

    public virtual IdentityMaster IdentityMaster { get; set; } = null!;
}
