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
using VolleySquad.Api.Domain;

namespace VolleySquad.Api.Services
{
    public class TeamService
    {
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
        // Logic ký thuật:
        //   i=0: round=0 (chẵn), position=0 => A
        //   i=1: round=0, position=1 => B  
        //   i=2: round=0, position=2 => C
        //   i=3: round=1 (lẻ), position=0 => C (ngược)
        //   i=4: round=1, position=1 => B
        //   i=5: round=1, position=2 => A
        public List<List<Member>> BalanceTeams(List<Member> players)
        {
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

            return new List<List<Member>> { teamA, teamB, teamC };
        }
    }
}