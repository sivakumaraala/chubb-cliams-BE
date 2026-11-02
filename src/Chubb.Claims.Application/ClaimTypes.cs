namespace Chubb.Claims.Application;

public enum ClaimType
{
    Motor,
    Property,
    Travel
}

public enum ClaimStatus
{
    Submitted,
    UnderReview,
    AwaitingInformation,
    Approved,
    Rejected,
    Settled
}

public enum ClaimHistoryAction
{
    Submitted,
    Assigned,
    InformationRequested,
    InformationProvided,
    Approved,
    Rejected,
    PaymentRecorded,
    Settled
}
