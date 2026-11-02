using Chubb.Claims.Application;

namespace Chubb.Claims.Application;

internal static class ClaimRules
{
    public static void ValidateText(string? text, string name, int maximum)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > maximum)
            throw new DomainValidationException($"{name} is required and must not exceed {maximum} characters.");
    }

    public static void ValidateMoney(decimal amount, string name)
    {
        if (amount <= 0 || amount > 9_999_999_999_999_999.99m || decimal.Round(amount, 2) != amount)
            throw new DomainValidationException($"{name} must be positive, fit decimal(18,2), and have at most two decimal places.");
    }

    public static void CheckVersion(Claim claim, string? supplied, IClaimCommandRepository repository)
    {
        byte[] bytes;
        try { bytes = Convert.FromBase64String(supplied ?? ""); }
        catch (FormatException) { throw new DomainValidationException("A valid rowVersion is required."); }
        if (bytes.Length != 8) throw new DomainValidationException("A SQL Server rowVersion is required.");
        if (!claim.RowVersion.AsSpan().SequenceEqual(bytes))
            throw new ClaimConflictException("This claim changed. Refresh it before trying again.");
        repository.SetExpectedVersion(claim, bytes);
    }

    public static Claim? Visible(Claim? claim, string actorId, bool isOfficer) =>
        claim is null || (!isOfficer && claim.OwnerId != actorId) ? null : claim;
}
