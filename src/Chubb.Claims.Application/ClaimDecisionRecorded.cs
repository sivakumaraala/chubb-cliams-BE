using System.Text.Json;

namespace Chubb.Claims.Application;

public sealed record ClaimDecisionRecorded(
    Guid EventId,
    int Version,
    Guid ClaimId,
    string ClaimReference,
    string OwnerId,
    string Outcome,
    DateTimeOffset OccurredAtUtc,
    string CorrelationId)
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public void Validate()
    {
        if (Version != 1 || EventId == Guid.Empty || ClaimId == Guid.Empty ||
            string.IsNullOrWhiteSpace(ClaimReference) || string.IsNullOrWhiteSpace(OwnerId) ||
            string.IsNullOrWhiteSpace(CorrelationId) || Outcome is not ("Approved" or "Rejected"))
            throw new InvalidDataException("Unsupported or incomplete decision event envelope.");
    }
}
