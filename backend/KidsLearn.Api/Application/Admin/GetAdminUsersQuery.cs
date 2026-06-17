using MediatR;
using Microsoft.EntityFrameworkCore;

public sealed record GetAdminUsersQuery : IRequest<IReadOnlyList<AdminUserResponse>>;

public sealed class GetAdminUsersQueryHandler : IRequestHandler<GetAdminUsersQuery, IReadOnlyList<AdminUserResponse>>
{
    private readonly AppDbContext _db;

    public GetAdminUsersQueryHandler(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<AdminUserResponse>> Handle(GetAdminUsersQuery query, CancellationToken cancellationToken)
    {
        return await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .Select(u => new AdminUserResponse(
                u.Id,
                u.Email,
                u.DisplayName,
                u.Role.ToString(),
                u.EmailVerified,
                u.ExternalProvider,
                u.CreatedAt,
                u.LastAccessAt))
            .ToListAsync(cancellationToken);
    }
}
