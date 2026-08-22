using FluentValidation;
using TechnoHub.Application.Auth.Dtos;
using TechnoHub.Domain.Constants;

namespace TechnoHub.Application.Auth.Validators;

public sealed class RegisterStaffRequestValidator : AbstractValidator<RegisterStaffRequest>
{
    public RegisterStaffRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email is not a valid address.")
            .MaximumLength(256);

        RuleFor(x => x.UserName)
            .MaximumLength(64)
            .Matches("^[a-zA-Z0-9._@+-]+$")
                .WithMessage("Username may only contain letters, digits and . _ @ + - characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.UserName));

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Full name is required.")
            .MaximumLength(128);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(32)
            .Matches(@"^[0-9+()\-\s]+$").WithMessage("Phone number contains invalid characters.")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        // Mirrors the Identity password policy configured in Infrastructure. Checked here too so
        // the caller gets one clean 400 with field-level messages instead of Identity's error codes.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(10).WithMessage("Password must be at least 10 characters.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.")
            .Matches("[^a-zA-Z0-9]").WithMessage("Password must contain a non-alphanumeric character.");

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role is required.")
            .Must(RoleNames.IsAssignable)
                .WithMessage($"Role must be one of: {string.Join(", ", RoleNames.Assignable)}. " +
                             "Admin accounts cannot be created through this endpoint.");

        RuleForEach(x => x.Scopes)
            .Must(ScopeNames.IsValid)
                .WithMessage("'{PropertyValue}' is not a known scope.")
            .When(x => x.Scopes is not null);

        RuleFor(x => x.Scopes)
            .Must(scopes => scopes!.Distinct(StringComparer.Ordinal).Count() == scopes!.Count)
                .WithMessage("Scopes must not contain duplicates.")
            .When(x => x.Scopes is not null);
    }
}
