using FluentValidation;

namespace Haven.Application.Features.Services.Commands.ImportFromRepository;

public class ImportFromRepositoryValidator : AbstractValidator<ImportFromRepositoryCommand>
{
    public ImportFromRepositoryValidator()
    {
        RuleFor(x => x.EnvironmentId).NotEmpty();
        RuleFor(x => x.Repository).NotEmpty();
        RuleFor(x => x.Branch).NotEmpty();
        RuleFor(x => x.FilePath).NotEmpty();
    }
}
