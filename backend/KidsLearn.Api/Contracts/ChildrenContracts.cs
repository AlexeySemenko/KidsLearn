public sealed record CreateChildRequest(string Name, int Grade, string? AccessCode);

public sealed record CreateChildWithEmailRequest(string Email, string Name, int Grade);

public sealed record UpdateChildRequest(string? Name, int? Grade, string? AccessCode);

public sealed record ChildResponse(Guid Id, Guid ParentId, string Name, int Grade, string? Email, bool IsPending = false);

public sealed record CreatedChildResponse(ChildResponse Child, string AccessCode);

public sealed record CreatedChildWithEmailResponse(ChildResponse Child);
