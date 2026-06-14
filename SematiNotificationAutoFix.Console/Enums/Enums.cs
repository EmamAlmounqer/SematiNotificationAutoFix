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
