namespace Chubb.Claims.Application;

public abstract class DomainException(string message) : Exception(message);

public sealed class DomainValidationException(string message) : DomainException(message);

public sealed class ClaimConflictException(string message) : DomainException(message);

public sealed class ClaimAccessDeniedException(string message) : DomainException(message);
