public sealed record CreateChildRequest(string Name, int Grade, string? AccessCode);

public sealed record UpdateChildRequest(string? Name, int? Grade, string? AccessCode);

public sealed record ChildResponse(Guid Id, Guid ParentId, string Name, int Grade);

public sealed record CreatedChildResponse(ChildResponse Child, string AccessCode);