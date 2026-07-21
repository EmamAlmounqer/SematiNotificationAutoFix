using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NobillCalls;
using SematiNotificationAutoFix.Console.Enums;
using SematiNotificationAutoFix.DAL.Data;
using SematiNotificationAutoFix.DAL.Models;

namespace SematiNotificationAutoFix.Console.Services;

public class NumberService
{
    private readonly ILogger<NumberService> _logger;
    private readonly ActivationDbContext _dbContext;
    private readonly NobillServiceClient _nobill;

    public NumberService(ILogger<NumberService> logger, ActivationDbContext dbContext, NobillServiceClient nobill)
    {
        _logger = logger;
        _dbContext = dbContext;
        _nobill = nobill;
    }

    public async Task<List<MsisdnCustomerData>> FetchMsisdnsAndAccountNumber(SematiNotification notification)
    {
        var personId = notification.IdNumber;
        var identityMaster = await _dbContext.IdentityMasters.AsNoTracking().FirstOrDefaultAsync(x => x.BorderNumber == personId)
                   ?? await _dbContext.IdentityMasters.AsNoTracking().FirstOrDefaultAsync(x => x.IdNumber == personId);

        if (identityMaster is not null)
        {
            var activations = await _dbContext.Activations.AsNoTracking()
                                                 .Where(x => x.IdentityMasterId == identityMaster.Id)
                                                 .OrderByDescending(x => x.CreatedOn)
                                                 .ToListAsync();

            if (activations is not null && activations.Count != 0)
            {
                var msisdns = activations.Select(x => new MsisdnCustomerData { Msisdn = x.MSISDN, AccountNumber = x.NobillAccountNumber }).ToList();
                // TODO check if number need termination
                return msisdns;
            }
            else
            {
                _logger.LogWarning("No Activation found for notification {NotificationId} with personId {PersonId} and IdentityMasterId {IdentityMasterId}", notification.Id, personId, identityMaster.Id);
            }
        }
        else
        {
            _logger.LogWarning("No IdentityMaster found for notification {NotificationId} with personId {PersonId}", notification.Id, personId);
        }

        var rawCallReports = await _dbContext.SematiCallReports.AsNoTracking()
                                                              .Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                              .OrderByDescending(x => x.TimeStamp)
                                                              .ToListAsync();


        if (rawCallReports is null || rawCallReports.Count == 0)
        {
            _logger.LogWarning("No CallReport with code 600 found for notification {NotificationId} with personId {PersonId}", notification.Id, personId);
            return [];
        }


        List<string> msisdnList = GetNumberNeedeTerminationFromCallReport(rawCallReports, personId);

        List<MsisdnCustomerData> msisdnCustomerDatas = [];

        foreach (var msisdn in msisdnList)
        {
            var accountData = await _nobill.GetCustomerDataAsync(msisdnList.FirstOrDefault());
            var accountNumber = accountData?.Body?.details?.CustomerNum;
            var customerId = accountData?.Body?.details?.CustomerID;

            if (string.IsNullOrEmpty(accountNumber))
            {
                _logger.LogWarning("No Nobill account number found for MSISDN {MSISDN} from CallReport for notification {NotificationId}", msisdnList.FirstOrDefault(), notification.Id);
                continue;
            }

            if (customerId != personId)
            {
                _logger.LogWarning("Nobill account data found for MSISDN {MSISDN} from CallReport for notification {NotificationId} does not match personId {PersonId}", msisdnList.FirstOrDefault(), notification.Id, personId);
                continue;
            }
            msisdnCustomerDatas.Add(new MsisdnCustomerData { Msisdn = msisdn, AccountNumber = accountNumber });
        }

        return msisdnCustomerDatas;
    }

    public async Task<bool> DoNumberNeedTerminationForPersonId(string personId, string msisdn)
    {
        var rawCallReports = await _dbContext.SematiCallReports.AsNoTracking()
                                                      .Where(x => x.code == "600" && (x.personId == personId || x.oldOwnerId == personId) && !string.IsNullOrEmpty(x.msisdn))
                                                      .OrderByDescending(x => x.TimeStamp)
                                                      .ToListAsync();


        var numbersNeedTermintaion = GetNumberNeedeTerminationFromCallReport(rawCallReports, personId);
        if (numbersNeedTermintaion is null) return false;
        if (numbersNeedTermintaion.Contains(msisdn)) return true;
        return false;
    }

    public List<string> GetNumberNeedeTerminationFromCallReport(List<SematiCallReport> rawCallReports, string personId)
    {
        var callReports = rawCallReports.GroupBy(x => x.msisdn!)
                                .ToDictionary(g => g.Key, g => g.ToList());

        if (callReports.Count == 0)
        {
            _logger.LogWarning("No CallReport with code 600 containing MSISDN found");
            return [];
        }

        RequestType[] acitvaiton = [RequestType.NewActivation, RequestType.TransferOperator];
        RequestType[] terminatRequestTypes = [RequestType.TerminateActivation, RequestType.CancelSIM];
        RequestType[] movedRequestTypes = [RequestType.TransferOwner];
        RequestType[] needTermination = [RequestType.NewActivation, RequestType.TransferOwner, RequestType.TransferOperator];

        List<string> msisdnList = [];

        foreach (var (msisdn, reports) in callReports)
        {
            var hasActivation = reports.Any(x =>
            {
                var requestTypeRaw = x.requestType;
                if (requestTypeRaw is null) return false;
                var requestType = (RequestType)requestTypeRaw;

                if (acitvaiton.Contains(requestType))
                    return true;

                return movedRequestTypes.Contains(requestType) && x.personId == personId;
            });

            if (!hasActivation)
            {
                _logger.LogTrace("MSISDN {MSISDN} has No Activation", msisdn);
                continue;
            }

            var sortedReports = reports.Where(x => x.code == "600").OrderByDescending(x => x.TimeStamp).ToList();
            var latestReport = sortedReports.FirstOrDefault();
            if (latestReport is null) continue;

            var requestTypeRaw = latestReport.requestType;
            if (requestTypeRaw is null) continue;
            var requestType = (RequestType)requestTypeRaw;

            var isNumberTerminated = terminatRequestTypes.Contains(requestType);
            var isNumberMoved = movedRequestTypes.Contains(requestType) && latestReport.personId != personId;

            if (isNumberTerminated || isNumberMoved)
            {
                _logger.LogTrace("Latest CallReport with code 600 for MSISDN {MSISDN} has requestType {RequestType}", msisdn, latestReport?.requestType);
                continue;
            }

            var isNumberNeedTermination = needTermination.Contains(requestType);
            if (isNumberNeedTermination)
                msisdnList.Add(msisdn);
        }

        return msisdnList;
    }
}

public class MsisdnCustomerData
{
    public required string Msisdn { get; set; }
    public string? AccountNumber { get; set; }
}