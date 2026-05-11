using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace VolleySquad.Api.Hubs
{
    // MatchHub: Cho phép client subscribe vào nhóm theo matchId
    // để nhận thông báo real-time khi số slot thay đổi.
    //
    // Client gọi: await connection.invoke("JoinMatchGroup", matchId)
    // Server push: hubContext.Clients.Group($"match-{matchId}").SendAsync("SlotUpdated", ...)
    [Authorize]
    public class MatchHub : Hub
    {
        private readonly ILogger<MatchHub> _logger;

        public MatchHub(ILogger<MatchHub> logger)
        {
            _logger = logger;
        }

        // Client gọi để nhận updates của 1 trận cụ thể
        public async Task JoinMatchGroup(string matchId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"match-{matchId}");
            _logger.LogDebug("Connection {ConnectionId} joined match group {MatchId}",
                Context.ConnectionId, matchId);
        }

        // Client gọi khi rời trang trận đấu
        public async Task LeaveMatchGroup(string matchId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"match-{matchId}");
        }
    }
}
