using FluentValidation;
using TechnoHub.Application.Auth.Dtos;

namespace TechnoHub.Application.Auth.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.EmailOrUsername)
            .NotEmpty().WithMessage("Email or username is required.")
            .MaximumLength(256);

        // Deliberately no complexity rules here — this is a login, not a registration. Telling a
        // caller their guess was "too short" leaks nothing useful but wastes a round trip.
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MaximumLength(256);
    }
}
