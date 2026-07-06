namespace SematiNotificationAutoFix.Console.Enums;

public enum SematiProcess : int
{
    Termination = 1,
    CancelSim = 2,
    ChangeSubscriptionType = 3
}

public enum RequestType
{
    VerifyCustomerId = -1,
    NewActivation = 1,
    AddSIM = 2,
    ChangeDefaultNumber = 3,
    TerminateActivation = 4,
    CancelSIM = 5,
    ChangeSubscriptionType = 6,
    TransferOwner = 17,
    TransferOperator = 18
}

public enum SematiNotificationStatus
{
    Locked = 1,
    Created = 2,
    Completed = 3,
    Canceled = 4,
    NotMsisdnsFound = 5,
    SematiNotUpdated = 6,
    TestingRecord = 7,
    UpdateOnly = 8,
    HasErrors = 9,
    UpdateAtSematiOnly = 12,
    ReverificationNumber = 13,
    CancelSemati = 14,
}
