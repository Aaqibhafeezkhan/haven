using FluentValidation;

using Haven.Application.Common;

namespace Haven.Application.Features.ApiTokens.Commands;

public class GenerateApiTokenValidator : AbstractValidator<GenerateApiTokenCommand>
{
    public GenerateApiTokenValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Token name is required.")
            .MaximumLength(100).WithMessage("Token name must not exceed 100 characters.");
        
        RuleFor(x => x.Scopes)
            .NotEmpty().WithMessage("At least one scope is required.")
            .Must(scopes => scopes.All(scope => !string.IsNullOrWhiteSpace(scope)))
            .WithMessage("Scopes must not contain empty or whitespace values.")
            .Must(scopes => scopes.All(scope => Permissions.All.Contains(scope)))
            .WithMessage("One or more scopes are invalid.");
        
        RuleFor(x => x.Expiration)
            .Must(expiration => expiration == null || expiration.Value > TimeSpan.Zero)
            .WithMessage("Expiration must be a positive time span if provided.");
    }
}