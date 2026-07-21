using Microsoft.Extensions.Logging.Abstractions;
using SematiNotificationAutoFix.Console.Processes;
using SematiNotificationAutoFix.Console.Services;
using SematiNotificationAutoFix.DAL.Models;

namespace SematiNotificationAutoFix.Tests;

public class FixNoNotificationActionProcessUnitTest
{
    NumberService _fixNoNotificationActionProcess;

    public FixNoNotificationActionProcessUnitTest()
    {
        _fixNoNotificationActionProcess = new(NullLogger<NumberService>.Instance, null!, null!);

    }

    [Fact]
    public void Test1()
    {
        var personId = "2119836076";
        var data = SematiCallReportTestData.GetBatch1().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.NotEmpty(result);
        Assert.Single(result, item => item == "966570657526");
    }

    [Fact]
    public void Test2()
    {
        var personId = "2602616639";
        var data = SematiCallReportTestData.GetBatch2().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.NotEmpty(result);
        Assert.Single(result, item => item == "966501285346");
    }

    [Fact]
    public void Test3()
    {
        var personId = "2636762854";
        var data = SematiCallReportTestData.GetBatch3().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.NotEmpty(result);
        Assert.Single(result, item => item == "966570376274");
    }

    [Fact]
    public void NoTerminationNeeded()
    {
        var personId = "2399048749";
        var data = SematiCallReportTestData.GetTwoActivationAndTwoTerminationNoTerminationNeeded().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.Empty(result);
    }

    [Fact]
    public void OneTerminationNeeded()
    {
        var personId = "2399048749";
        var data = SematiCallReportTestData.GetTwoActivationAndTwoTerminationOneTerminationNeeded().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.NotEmpty(result);
        Assert.Single(result, item => item == "966570840952");
    }

    [Fact]
    public void TwoTerminationNeeded()
    {
        var personId = "2399048749";
        var data = SematiCallReportTestData.GetTwoActivationAndTwoTerminationTwoTerminationNeeded().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        NumberService fixNoNotificationActionProcess = new(NullLogger<NumberService>.Instance, null!, null!);

        var result = fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);
        Assert.NotEmpty(result);
        Assert.Contains("966570840952", result);
        Assert.Contains("966570330278", result);
    }

    [Fact]
    public void CancilSimNoTerminationNeeded()
    {
        var personId = "2394361253";
        var data = SematiCallReportTestData.GetActivationWithCancelSIMNoTerminationNeeded().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.Empty(result);
    }

    [Fact]
    public void GetActivationWithCancelSIMCancelWithNoTermintaion()
    {
        var personId = "2394361253";
        var data = SematiCallReportTestData.GetActivationWithCancelSIMCancelWithNoTermintaion().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.Empty(result);
        //Assert.NotEmpty(result);
        //Assert.Single(result, item => item == "966570456453");
    }

    [Fact]
    public void GetWithTransferOwnerFromSourceAndCancelSim()
    {
        var personId = "2570719076";
        var data = SematiCallReportTestData.GetWithTransferOwnerFromSourceAndCancelSim().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.Empty(result);
    }

    [Fact]
    public void GetNeedOneTermination()
    {
        var personId = "2394734855";
        var data = SematiCallReportTestData.GetNeedOneTermination().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.NotEmpty(result);
        Assert.Single(result, item => item == "966570150979");
    }

    [Fact]
    public void GetNumberNeed3Termination()
    {
        var personId = "2457644132";
        var data = SematiCallReportTestData.GetNumberNeed3Termination().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.NotEmpty(result);
        Assert.Equal(3, result.Count);
        Assert.Contains("966571830012", result);
        Assert.Contains("966572549175", result);
        Assert.Contains("966573072651", result);
    }
    

    [Fact]
    public void GetWithTransferOwnerAndOther()
    {
        var personId = "1115434191";
        var data = SematiCallReportTestData.GetWithTransferOwnerAndOther().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.Empty(result);

    }
    
    [Fact]
    public void GetNumberWithTransferOwnerAndCancelSimNoNeedForActivation()
    {
        var personId = "2309379390";
        var data = SematiCallReportTestData.GetNumberWithTransferOwnerAndCancelSimNoNeedForActivation().Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToList();

        var result = _fixNoNotificationActionProcess.GetNumberNeedeTerminationFromCallReport(data, personId);

        Assert.Empty(result);
    }
}
