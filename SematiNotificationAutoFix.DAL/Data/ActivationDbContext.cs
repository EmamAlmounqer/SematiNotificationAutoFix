using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using SematiNotificationAutoFix.DAL.Models;

namespace SematiNotificationAutoFix.DAL.Data;

public partial class ActivationDbContext : DbContext
{
    public ActivationDbContext()
    {
    }

    public ActivationDbContext(DbContextOptions<ActivationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Activation> Activations { get; set; }

    public virtual DbSet<IdentityMaster> IdentityMasters { get; set; }

    public virtual DbSet<SematiCallReport> SematiCallReports { get; set; }

    public virtual DbSet<SematiNotification> SematiNotifications { get; set; }

    public virtual DbSet<SematiNotificationAction> SematiNotificationActions { get; set; }

    public virtual DbSet<SematiNotificationActionStep> SematiNotificationActionSteps { get; set; }

    public virtual DbSet<SematiServiceCallLog> SematiServiceCallLogs { get; set; }

    public virtual DbSet<SematiTerminateNumber> SematiTerminateNumbers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Activation>(entity =>
        {
            entity.ToTable("Activation", tb => tb.HasTrigger("trgAfterUpdateActivation"));

            entity.HasIndex(e => e.NobillAccountNumber, "AccNo");

            entity.HasIndex(e => e.IMSI, "IX_Activation").IsUnique();

            entity.HasIndex(e => new { e.CreatedOn, e.IdentityMasterId }, "IX_Activation_CreatedOn_IdentityMasterId");

            entity.HasIndex(e => e.IsActive, "IX_Activation_IsActive");

            entity.HasIndex(e => e.MSISDN, "IX_Activation_MSISDN");

            entity.HasIndex(e => new { e.MSISDN, e.IsActive, e.IdentityVerificationStatusId, e.IdentityVerificationStatusUpdateDate }, "IX_Activation_MSISDN_IsActive_IdentityVerificationStatusId_IdentityVerificationStatusUpdateDate");

            entity.HasIndex(e => e.PortOutDateTime, "IX_Activation_PortOutDateTime");

            entity.HasIndex(e => new { e.SubscriptionTypeId, e.CreatedOn }, "IX_Activation_SubscriptionTypeId_CreatedOn");

            entity.HasIndex(e => new { e.CreatedOn, e.DealerCode }, "_dta_index_Activation_28_2005582183__K2_K8_8066");

            entity.HasIndex(e => new { e.IdentityMasterId, e.IsActive, e.IsPrimary }, "_dta_index_Activation_28_2005582183__K4_K9_K34_9987").HasFillFactor(80);

            entity.Property(e => e.ActivationChannel)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CRM_AccountID).HasMaxLength(150);
            entity.Property(e => e.CRM_ContactID).HasMaxLength(150);
            entity.Property(e => e.CreatedOn).HasColumnType("datetime");
            entity.Property(e => e.DealerCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DistributorCode).HasMaxLength(100);
            entity.Property(e => e.Email).HasMaxLength(150);
            entity.Property(e => e.FirstChargeableEventReceivedTime).HasColumnType("datetime");
            entity.Property(e => e.FirstEventReceivedDate).HasColumnType("datetime");
            entity.Property(e => e.FormSerial)
                .HasMaxLength(22)
                .IsUnicode(false);
            entity.Property(e => e.ICCID)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IMSI)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IdentityVerificationStatusUpdateDate).HasColumnType("datetime");
            entity.Property(e => e.KitID)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.LastUpdateDate).HasColumnType("datetime");
            entity.Property(e => e.Latitude)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.LocationID).HasMaxLength(500);
            entity.Property(e => e.Longitude)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.MSISDN)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.NobillAccountNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.NobillCustomerNo)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PUK)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.PaperStatusUpdateDate).HasColumnType("datetime");
            entity.Property(e => e.PhysicalFormOfficeReceivedDate).HasColumnType("datetime");
            entity.Property(e => e.PhysicalFormReceivedSalesCode).HasMaxLength(50);
            entity.Property(e => e.PhysicalFormSalesReceivedDate).HasColumnType("datetime");
            entity.Property(e => e.PortOutDateTime).HasColumnType("datetime");
            entity.Property(e => e.QualityGrade)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.Receipt_Voucher_Number)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.RejectionReason).HasMaxLength(500);
            entity.Property(e => e.ResumissionDate).HasColumnType("datetime");
            entity.Property(e => e.TerminationDate).HasColumnType("datetime");

            entity.HasOne(d => d.IdentityMaster).WithMany(p => p.Activations)
                .HasForeignKey(d => d.IdentityMasterId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Activation_IdentityMaster");
        });

        modelBuilder.Entity<IdentityMaster>(entity =>
        {
            entity.ToTable("IdentityMaster");

            entity.HasIndex(e => e.BorderNumber, "_dta_index_IdentityMaster_28_990626572__K39_1");

            entity.HasIndex(e => e.IdNumber, "_dta_index_IdentityMaster_28_990626572__K4");

            entity.Property(e => e.BorderNumber).HasMaxLength(50);
            entity.Property(e => e.ContactNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.DealerCode)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ElmCallReferenceNumber).HasMaxLength(50);
            entity.Property(e => e.ElmLogId).HasMaxLength(50);
            entity.Property(e => e.Email).HasMaxLength(100);
            entity.Property(e => e.FirstName).HasMaxLength(150);
            entity.Property(e => e.FirstNameTrasnlated).HasMaxLength(150);
            entity.Property(e => e.Gender).HasMaxLength(10);
            entity.Property(e => e.HijriDateOfBirth).HasMaxLength(10);
            entity.Property(e => e.HijriVisaIssueDate).HasMaxLength(10);
            entity.Property(e => e.IdExpiryDateHijri).HasMaxLength(50);
            entity.Property(e => e.IdNumber).HasMaxLength(50);
            entity.Property(e => e.LastName).HasMaxLength(150);
            entity.Property(e => e.LastNameTrasnlated).HasMaxLength(150);
            entity.Property(e => e.MaritalStatus).HasMaxLength(10);
            entity.Property(e => e.MobileNumberAuthenticationTxNo).HasMaxLength(50);
            entity.Property(e => e.NationalityCode).HasMaxLength(3);
            entity.Property(e => e.RequestType).HasMaxLength(50);
            entity.Property(e => e.ResponseMessage).HasMaxLength(100);
            entity.Property(e => e.SecondName).HasMaxLength(150);
            entity.Property(e => e.SecondNameTrasnlated).HasMaxLength(150);
            entity.Property(e => e.TCN).HasMaxLength(50);
            entity.Property(e => e.ThirdName).HasMaxLength(150);
            entity.Property(e => e.ThirdNameTrasnlated).HasMaxLength(150);
        });

        modelBuilder.Entity<SematiCallReport>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("SematiCallReport");

            entity.HasIndex(e => e.msisdn, "IX_SematiCallReport_msisdn");

            entity.HasIndex(e => new { e.oldOwnerId, e.msisdn, e.requestType }, "IX_SematiCallReport_oldOwnerId_msisdn_requestType");

            entity.HasIndex(e => e.PrimaryIMSI, "NonClusteredIndex-20241202-133319");

            entity.HasIndex(e => e.SetaLogsId, "NonClusteredIndex-20250304-101957");

            entity.HasIndex(e => e.msisdn, "NonClusteredIndex-MSISDN");

            entity.HasIndex(e => e.personId, "NonClusteredIndex-PersonId");

            entity.HasIndex(e => e.TimeStamp, "NonClusteredIndex-TimeStamp");

            entity.Property(e => e.Channel)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.DealerCode)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.IdType)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.PrimaryICCD)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.PrimaryIMSI)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.SecondaryICCD)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.SecondaryIMSI)
                .HasMaxLength(31)
                .IsUnicode(false);
            entity.Property(e => e.TimeStamp).HasColumnType("datetime");
            entity.Property(e => e.apiKey)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.branchAddress).HasMaxLength(100);
            entity.Property(e => e.code)
                .HasMaxLength(4)
                .IsUnicode(false);
            entity.Property(e => e.deviceId)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.employeeId)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.employeeUsername)
                .HasMaxLength(25)
                .IsUnicode(false);
            entity.Property(e => e.isDefault)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.message)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.msisdn)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.msisdnType)
                .HasMaxLength(5)
                .IsUnicode(false);
            entity.Property(e => e.oldOwnerId)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.operatorTCN)
                .HasMaxLength(40)
                .IsUnicode(false);
            entity.Property(e => e.orgId)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.personId)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.region)
                .HasMaxLength(2)
                .IsUnicode(false);
            entity.Property(e => e.tcn)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<SematiNotification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_NotificationId");

            entity.ToTable("SematiNotification");

            entity.HasIndex(e => e.Status, "_dta_index_SematiNotification_12_1027000377__K5_1_2_3_4_6_7_8_9");

            entity.Property(e => e.CancelationReason)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.IdNumber).HasMaxLength(50);
            entity.Property(e => e.MSISDN)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ReceivedDateTime).HasColumnType("datetime");
        });

        modelBuilder.Entity<SematiNotificationAction>(entity =>
        {
            entity.ToTable("SematiNotificationAction");

            entity.HasIndex(e => new { e.MSISDN, e.SematiNotificationId, e.SematiNotificationActionStepId }, "SematiNotificationAction_index_451498_191220200215");

            entity.HasIndex(e => new { e.Status, e.RetriesCount, e.SematiNotificationActionStepId, e.SematiNotificationId }, "_dta_index_SematiNotificationAction_12_1219001061__K12_K18_K15_K13_1_2_3_4_5_6_7_8_9_10_11_14_16_17_19");

            entity.Property(e => e.AccountNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.CancelationReason)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime");
            entity.Property(e => e.ExecutedAt).HasColumnType("datetime");
            entity.Property(e => e.ExpectedActionDate).HasColumnType("datetime");
            entity.Property(e => e.LastUpdated).HasColumnType("datetime");
            entity.Property(e => e.MSISDN)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SematiActionCode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.SematiActionTcn)
                .HasMaxLength(500)
                .IsUnicode(false);
            entity.Property(e => e.SematiErrorMessage).HasMaxLength(4000);
            entity.Property(e => e.SematiNotifiedAt).HasColumnType("datetime");
            entity.Property(e => e.SematiUpdateCode)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.SematiUpdateTcn)
                .HasMaxLength(500)
                .IsUnicode(false);

            entity.HasOne(d => d.SematiNotificationActionStep).WithMany(p => p.SematiNotificationActions)
                .HasForeignKey(d => d.SematiNotificationActionStepId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SematiNotificationAction_SematiNotificationActionStep");

            entity.HasOne(d => d.SematiNotification).WithMany(p => p.SematiNotificationActions)
                .HasForeignKey(d => d.SematiNotificationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_SematiNotificationAction_SematiNotificationStatus");
        });

        modelBuilder.Entity<SematiNotificationActionStep>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_SN_ActionType");

            entity.ToTable("SematiNotificationActionStep");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.Action).HasMaxLength(250);
            entity.Property(e => e.ArabicMessageTemplate).HasMaxLength(1000);
            entity.Property(e => e.EnglishMessageTemplate).HasMaxLength(1000);
        });

        modelBuilder.Entity<SematiServiceCallLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK_SematiServiceCalls_20250714");

            entity.ToTable("SematiServiceCallLog");

            entity.HasIndex(e => e.Code, "IX_SematiServiceCallLog_Code");

            entity.HasIndex(e => new { e.Code, e.DealerCode, e.Timestamp }, "IX_SematiServiceCallLog_Code_DealerCode_Timestamp");

            entity.HasIndex(e => e.SetaLogsId, "NonClusteredIndex-20250728-112140");

            entity.Property(e => e.Channel).HasMaxLength(50);
            entity.Property(e => e.DealerCode).HasMaxLength(50);
            entity.Property(e => e.Operation).HasMaxLength(50);
            entity.Property(e => e.RequestType).HasMaxLength(50);
            entity.Property(e => e.ResponseText).HasMaxLength(1000);
            entity.Property(e => e.TCN).HasMaxLength(50);
            entity.Property(e => e.Timestamp).HasColumnType("datetime");
            entity.Property(e => e.Url).HasMaxLength(100);
        });

        modelBuilder.Entity<SematiTerminateNumber>(entity =>
        {
            entity.ToTable("SematiTerminateNumber");

            entity.Property(e => e.ExecutionTime).HasColumnType("datetime");
            entity.Property(e => e.ICCID)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.IDNumber)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.IMSI)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.MSISDN)
                .HasMaxLength(20)
                .IsUnicode(false);
            entity.Property(e => e.OperatorTCN)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.ProcessId).HasDefaultValue(1);
            entity.Property(e => e.SubscriptionType)
                .HasMaxLength(1)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.TCN)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
