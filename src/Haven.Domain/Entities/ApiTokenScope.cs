namespace Haven.Domain.Entities;

public class ApiTokenScope : Entity
{
    public Guid ApiTokenId { get; set; }
    public string Scope { get; set; } = string.Empty;

    public ApiToken? ApiToken { get; set; }
}