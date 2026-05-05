namespace MediBook.Payment.API.DTOs;

/// <summary>
/// Minimal projection of auth-service UserDto used for notification lookup.
/// Fields match the JSON returned by GET /api/v1/auth/users/{id}.
/// </summary>
public sealed record AuthUserDto(
    Guid   Id,
    string FullName,
    string Email
);
