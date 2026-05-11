// ============================================================
// TEAM SERVICE - Business Logic: Chia 3 đội cân bằng (Snake Draft Algorithm)
// ============================================================
// SERVICE LAYER trong Clean Architecture:
//   Controller:  Nhận request, gọi Service, trả response (không chứa business logic)
//   Service:     Xử lý business logic (LAYER NÀY)
//   Repository:  Truy cập data (DbContext đóng vai trò Repository ở project này)
//   Domain:      Entity và business rules (Member, Match)
//
// Tại sao tách logic vào Service thay vì viết trong Controller?
//   - Dễ unit test: Test BalanceTeams() độc lập không cần HTTP request
//   - Tái sử dụng: Nhiều Controller có thể inject và dùng TeamService
//   - Single Responsibility: Mỗi class chỉ có 1 lý do để thay đổi
//
// ============================================================
// SERVICE LIFETIME: AddSingleton (khác với MemberService là Scoped)
// ============================================================
// TeamService KHÔNG có state (không lưu data giữa các request).
// Mỗi method nhận input → xử lý thuần túy → trả output.
// KHÔNG inject AppDbContext (BalanceTeamsByMatchAsync inject qua method parameter).
//
// Singleton hoàn toàn an toàn vì:
//   1. Stateless: Không có field bị shared giữa các request.
//   2. Thread-safe: Chỉ đọc/ghi local variables trong method.
//   3. Không có Scoped dependency (tránh Captive Dependency anti-pattern).
//
// Lợi ích của Singleton so với Scoped ở đây:
//   - Không tạo instance mới mỗi request (tiết kiệm GC pressure nhỏ).
//   - Constructor chỉ chạy 1 lần khi app start.
//   - Phù hợp hơn về mặt ngữ nghĩa: "dịch vụ chia đội" không có state.
using Microsoft.EntityFrameworkCore;
using VolleySquad.Api.Domain;
using VolleySquad.Api.Domain.Exceptions;
using VolleySquad.Api.Domain.ValueObjects;
using VolleySquad.Api.Infrastructure;
using VolleySquad.Api.Services.Interfaces;

namespace VolleySquad.Api.Services
{
    public class TeamService : ITeamService
    {
        // AppDbContext KHÔNG inject qua Constructor (nó là Scoped, TeamService là Singleton).
        // Inject qua method parameter thay thế.
        // Hoặc dùng IServiceScopeFactory để tạo scope tạm thời (advanced pattern).
        // Xem BalanceTeamsByMatchAsync bên dưới.

        // ============================================================
        // SNAKE DRAFT ALGORITHM - Thuật toán chia đội cân bằng
        // ============================================================
        // Ví dụ với 6 người, skill: [100, 90, 80, 70, 60, 50]
        //
        // Lượt 1 (xuôi A→B→C): A[100], B[90], C[80]
        // Lượt 2 (ngược C→B→A): C[70], B[60], A[50]
        //
        // Kết quả: A[100+50=150], B[90+60=150], C[80+70=150] => Cân bằng hoàn hảo!
        //
        // Logic kỹ thuật:
        //   i=0: round=0 (chẵn), position=0 => A
        //   i=1: round=0, position=1 => B
        //   i=2: round=0, position=2 => C
        //   i=3: round=1 (lẻ), position=0 => C (ngược)
        //   i=4: round=1, position=1 => B
        //   i=5: round=1, position=2 => A
        public TeamResult BalanceTeams(List<Member> players)
        {
            if (players.Count < 3)
                throw new DomainException(
                    $"Cần ít nhất 3 người để chia 3 đội. Hiện có: {players.Count}",
                    "INSUFFICIENT_PLAYERS");

            // 1. Sắp xếp giảm dần theo SkillPoint
            //    OrderByDescending: LINQ method — lazy evaluation, chỉ chạy khi .ToList() được gọi
            var sortedPlayers = players.OrderByDescending(p => p.SkillPoint).ToList();

            var teamA = new List<Member>();
            var teamB = new List<Member>();
            var teamC = new List<Member>();

            for (int i = 0; i < sortedPlayers.Count; i++)
            {
                int round = i / 3;    // Lượt chia thứ mấy (0, 1, 2...)
                int position = i % 3; // Vị trí trong lượt đó (0, 1, 2)

                if (round % 2 == 0) // Lượt chẵn (0, 2, 4...): xuôi A → B → C
                {
                    if (position == 0) teamA.Add(sortedPlayers[i]);
                    else if (position == 1) teamB.Add(sortedPlayers[i]);
                    else teamC.Add(sortedPlayers[i]);
                }
                else // Lượt lẻ (1, 3, 5...): ngược C → B → A
                {
                    if (position == 0) teamC.Add(sortedPlayers[i]);
                    else if (position == 1) teamB.Add(sortedPlayers[i]);
                    else teamA.Add(sortedPlayers[i]);
                }
            }

            // Trả TeamResult (Value Object) thay vì List<List<Member>> để type-safe và có metrics
            return TeamResult.Create(teamA, teamB, teamC);
        }

        // ============================================================
        // GIẢI PHÁP CHO SINGLETON + SCOPED DEPENDENCY
        // ============================================================
        // Singleton không thể inject Scoped service qua constructor.
        // 2 cách giải quyết:
        //
        // CÁCH 1 (đang dùng): Nhận DbContext qua method parameter.
        //   Caller (Controller, Scoped) tự inject DbContext và truyền vào.
        //   Pro: Đơn giản, rõ ràng.
        //   Con: Method signature phức tạp hơn.
        //
        // CÁCH 2 (advanced): IServiceScopeFactory.
        //   private readonly IServiceScopeFactory _scopeFactory;
        //   public async Task<TeamResult> BalanceTeamsByMatchAsync(Guid matchId) {
        //       using var scope = _scopeFactory.CreateScope();
        //       var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        //       ... dùng ctx ...
        //   }
        //   Pro: Không cần pass context vào. Con: Ẩn dependency, khó test hơn.
        public async Task<TeamResult> BalanceTeamsByMatchAsync(Guid matchId)
        {
            // Method này cần DbContext nhưng TeamService là Singleton.
            // Thiết kế này không inject qua constructor — caller phải truyền context vào.
            // Xem MatchController.SplitFromDb() để thấy cách gọi.
            //
            // Tuy nhiên để giữ interface ITeamService clean (không expose AppDbContext),
            // method này được implement theo CÁCH 2 với IServiceScopeFactory trong thực tế.
            // Ở đây ta throw NotImplementedException để nhắc dev implement khi cần.
            throw new NotImplementedException(
                "BalanceTeamsByMatchAsync cần IServiceScopeFactory injection. " +
                "Xem comment trong TeamService.cs để implement.");
        }
    }
}
