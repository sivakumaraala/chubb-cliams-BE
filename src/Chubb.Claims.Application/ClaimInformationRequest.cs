namespace Chubb.Claims.Application;

public sealed class ClaimInformationRequest
{
    private ClaimInformationRequest() { }
    internal ClaimInformationRequest(Guid claimId, string officerId, string question, DateTimeOffset now)
    {
        Id = Guid.NewGuid(); ClaimId = claimId; OfficerId = officerId;
        Question = question; RequestedAtUtc = now.ToUniversalTime();
    }
    public Guid Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public string OfficerId { get; private set; } = string.Empty;
    public string Question { get; private set; } = string.Empty;
    public DateTimeOffset RequestedAtUtc { get; private set; }
    public string? Response { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }
    internal void Answer(string response, DateTimeOffset now)
    {
        if (Response is not null) throw new ClaimConflictException("This request has already been answered.");
        Response = response; RespondedAtUtc = now.ToUniversalTime();
    }
}
