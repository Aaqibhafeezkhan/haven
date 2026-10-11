namespace Haven.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAdmin { get; }
    string? ApiToken { get; }
    bool IsBackgroundContext { get; }
}