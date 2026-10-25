using MediatR;
using StackTrack.Application.Errors;

namespace StackTrack.Application.Boards;

public class BoardAccessBehavior<TRequest, TResponse>(IBoardAccess access)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    // Same message for an unknown board and a board the user cannot see, so the two are indistinguishable.
    const string NotFoundMessage = "The board was not found.";

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var write = request is IWriteRequest;
        if (request is IBoardScoped board)
            await Check(board.BoardId, write, ct);

        return await next(ct);
    }

    async Task Check(Guid boardId, bool write, CancellationToken ct)
    {
        var level = await access.GetAsync(boardId, ct);
        if (level == BoardAccessLevel.None) throw new NotFoundException(NotFoundMessage);
        if (level == BoardAccessLevel.ReadOnly && write) throw new ForbiddenException("This board is read-only for you.");
    }
}
