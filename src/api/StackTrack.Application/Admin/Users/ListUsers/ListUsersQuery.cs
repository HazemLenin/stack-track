using FluentValidation;
using MediatR;

namespace StackTrack.Application.Admin.Users.ListUsers;

public record ListUsersQuery(string? Search = null, string Sort = "name", string Dir = "asc", int Page = 1)
    : IRequest<UserPage>;

public class ListUsersQueryValidator : AbstractValidator<ListUsersQuery>
{
    public ListUsersQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.Sort).Must(s => s is "name" or "created").WithMessage("Sort must be name or created");
        RuleFor(q => q.Dir).Must(d => d is "asc" or "desc").WithMessage("Dir must be asc or desc");
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
    }
}

public class ListUsersQueryHandler(IUserAdminRepository users) : IRequestHandler<ListUsersQuery, UserPage>
{
    public const int PageSize = 20;

    public Task<UserPage> Handle(ListUsersQuery request, CancellationToken ct) =>
        users.ListAsync(request.Search?.Trim(), request.Sort, request.Dir, request.Page, PageSize, ct);
}
