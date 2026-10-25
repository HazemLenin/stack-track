using FluentValidation;
using MediatR;

namespace StackTrack.Application.Auth.Register;

// No Role property: a "role" field in the request body is dropped by model binding.
public record RegisterCommand(string DisplayName, string Email, string Password, string ConfirmPassword)
    : IRequest<IssuedTokens>;

public class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(c => c.DisplayName)
            .Must(n => (n ?? "").Trim().Length is >= 2 and <= 50)
            .WithMessage("Display name must be 2 to 50 characters");

        RuleFor(c => c.Email)
            .NotEmpty().MaximumLength(320).EmailAddress();

        RuleFor(c => c.Password)
            .MinimumLength(8).WithMessage("At least 8 characters")
            .MaximumLength(128).WithMessage("At most 128 characters")
            .Matches("[A-Z]").WithMessage("One uppercase letter")
            .Matches("[a-z]").WithMessage("One lowercase letter")
            .Matches("[0-9]").WithMessage("One digit");

        RuleFor(c => c.ConfirmPassword)
            .Equal(c => c.Password).WithMessage("Passwords do not match");
    }
}
