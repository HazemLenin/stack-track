using FluentValidation;
using MediatR;
using StackTrack.Application.Auth;

namespace StackTrack.Application.Admin.Users.DeactivateUser;

public record DeactivateUserCommand(Guid Id) : IRequest;

public class DeactivateUserCommandValidator : AbstractValidator<DeactivateUserCommand>
{
    public DeactivateUserCommandValidator(ICurrentUser current) =>
        RuleFor(c => c.Id).NotEqual(current.Id).WithMessage("You cannot deactivate your own account");
}

public class DeactivateUserCommandHandler(IUserAdminRepository users) : IRequestHandler<DeactivateUserCommand>
{
    public async Task Handle(DeactivateUserCommand request, CancellationToken ct) =>
        (await users.SetActiveAsync(request.Id, false, ct)).ThrowIfRefused();
}
