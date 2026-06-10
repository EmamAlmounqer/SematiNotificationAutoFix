using System;
using System.Collections.Generic;

namespace SematiNotificationAutoFix.DAL.Models;

public partial class IdentityMaster
{
    public int Id { get; set; }

    public DateTime CreationDate { get; set; }

    public int IdTypeId { get; set; }

    public string IdNumber { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string SecondName { get; set; } = null!;

    public string ThirdName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public DateTime DateOfBirth { get; set; }

    public string HijriDateOfBirth { get; set; } = null!;

    public string NationalityCode { get; set; } = null!;

    public DateTime? IdExpiryDate { get; set; }

    public string? IdExpiryDateHijri { get; set; }

    public string? HijriVisaIssueDate { get; set; }

    public bool? legalStatus { get; set; }

    public string DealerCode { get; set; } = null!;

    public string ElmCallReferenceNumber { get; set; } = null!;

    public string ElmLogId { get; set; } = null!;

    public string? FirstNameTrasnlated { get; set; }

    public string? SecondNameTrasnlated { get; set; }

    public string? ThirdNameTrasnlated { get; set; }

    public string? LastNameTrasnlated { get; set; }

    public string? Email { get; set; }

    public byte StatusId { get; set; }

    public DateTime? StatusDate { get; set; }

    public DateTime? StatusExpiry { get; set; }

    public bool IsLockedForProcessing { get; set; }

    public DateTime? LockDate { get; set; }

    public long? Occupation { get; set; }

    public long? Sponsor { get; set; }

    public DateTime? IdIssueDate { get; set; }

    public string? MaritalStatus { get; set; }

    public string? Gender { get; set; }

    public string? TCN { get; set; }

    public string? RequestType { get; set; }

    public int? ResponseCode { get; set; }

    public string? ResponseMessage { get; set; }

    public string? MobileNumberAuthenticationTxNo { get; set; }

    public string? BorderNumber { get; set; }

    public int? AuthenticationTypeID { get; set; }

    public int? CityId { get; set; }

    public int? NationalAddressId { get; set; }

    public string? ContactNumber { get; set; }

    public virtual ICollection<Activation> Activations { get; set; } = new List<Activation>();
}
