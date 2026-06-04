public sealed record CreateChildRequest(string Name, int Grade);

public sealed record UpdateChildRequest(string? Name, int? Grade);

public sealed record ChildResponse(Guid Id, Guid ParentId, string Name, int Grade);