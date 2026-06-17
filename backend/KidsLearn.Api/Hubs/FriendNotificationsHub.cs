using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

[Authorize(Policy = "ChildOnly")]
public class FriendNotificationsHub(AppDbContext db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var childId = Context.User is null ? null : await ApiEndpointHelpers.ResolveChildIdAsync(db, Context.User);
        if (childId.HasValue)
            await Groups.AddToGroupAsync(Context.ConnectionId, childId.Value.ToString("N"));

        await base.OnConnectedAsync();
    }
}
