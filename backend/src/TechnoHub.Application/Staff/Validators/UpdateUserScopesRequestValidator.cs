using FluentValidation;
using TechnoHub.Application.Staff.Dtos;
using TechnoHub.Domain.Constants;

namespace TechnoHub.Application.Staff.Validators;

public sealed class UpdateUserScopesRequestValidator : AbstractValidator<UpdateUserScopesRequest>
{
    public UpdateUserScopesRequestValidator()
    {
        // An empty array is valid and meaningful: it strips every permission from the account.
        RuleFor(x => x.Scopes)
            .NotNull().WithMessage("Scopes is required. Send an empty array to remove all scopes.");

        RuleForEach(x => x.Scopes)
            .Must(ScopeNames.IsValid).WithMessage("'{PropertyValue}' is not a known scope.")
            .When(x => x.Scopes is not null);

        RuleFor(x => x.Scopes)
            .Must(scopes => scopes.Distinct(StringComparer.Ordinal).Count() == scopes.Count)
                .WithMessage("Scopes must not contain duplicates.")
            .When(x => x.Scopes is not null);
    }
}

public sealed class UpdateUserStatusRequestValidator : AbstractValidator<UpdateUserStatusRequest>
{
    public UpdateUserStatusRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(256)
            .When(x => !string.IsNullOrWhiteSpace(x.Reason));
    }
}
