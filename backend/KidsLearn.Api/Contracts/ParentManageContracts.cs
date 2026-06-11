public sealed record LinkParentAccountRequest(string Email);

public sealed record LinkedParentResponse(Guid ParentId, string Email, DateTime LinkedAt);
