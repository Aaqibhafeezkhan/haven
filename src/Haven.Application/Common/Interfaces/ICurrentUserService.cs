namespace Haven.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAdmin { get; }
    bool IsApiToken { get; }
    IReadOnlyCollection<string> Scopes { get; }
    bool IsBackgroundContext { get; }
}