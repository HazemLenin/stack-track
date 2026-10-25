using FluentValidation;
using MediatR;
using StackTrack.Application.Auth;
using StackTrack.Domain.Entities;

namespace StackTrack.Application.Admin.Users.ChangeUserRole;

public record ChangeUserRoleCommand(Guid Id, string Role) : IRequest;

public class ChangeUserRoleCommandValidator : AbstractValidator<ChangeUserRoleCommand>
{
    public ChangeUserRoleCommandValidator(ICurrentUser current)
    {
        RuleFor(c => c.Id).NotEqual(current.Id).WithMessage("You cannot change your own role");
        RuleFor(c => c.Role).Must(r => Enum.GetNames<UserRole>().Contains(r)).WithMessage("Role must be Admin or Member");
    }
}

public class ChangeUserRoleCommandHandler(IUserAdminRepository users) : IRequestHandler<ChangeUserRoleCommand>
{
    public async Task Handle(ChangeUserRoleCommand request, CancellationToken ct) =>
        (await users.SetRoleAsync(request.Id, Enum.Parse<UserRole>(request.Role), ct)).ThrowIfRefused();
}
