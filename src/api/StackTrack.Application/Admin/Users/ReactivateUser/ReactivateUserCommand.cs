using FluentValidation;
using MediatR;

namespace StackTrack.Application.Admin.Users.ReactivateUser;

public record ReactivateUserCommand(Guid Id) : IRequest;

public class ReactivateUserCommandValidator : AbstractValidator<ReactivateUserCommand>
{
    public ReactivateUserCommandValidator() => RuleFor(c => c.Id).NotEmpty();
}

public class ReactivateUserCommandHandler(IUserAdminRepository users) : IRequestHandler<ReactivateUserCommand>
{
    public async Task Handle(ReactivateUserCommand request, CancellationToken ct) =>
        (await users.SetActiveAsync(request.Id, true, ct)).ThrowIfRefused();
}
