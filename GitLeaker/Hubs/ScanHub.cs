using Microsoft.AspNetCore.SignalR;

namespace GitLeaker.Hubs;

public class ScanHub : Hub
{
    public async Task JoinScan(string scanId)
        => await Groups.AddToGroupAsync(Context.ConnectionId, scanId);
}