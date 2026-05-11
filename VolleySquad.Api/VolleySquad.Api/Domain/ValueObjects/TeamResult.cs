// ============================================================
// TEAM RESULT - Value Object (Kết quả chia 3 đội)
// ============================================================
// VALUE OBJECT vs ENTITY - Sự khác biệt quan trọng trong Domain Design:
//
//   ENTITY:       Có Identity (Id). Hai Member khác nhau dù cùng tên.
//                 Mutable — có thể thay đổi state (balance, skill...).
//
//   VALUE OBJECT: Không có Identity. Chỉ định nghĩa bởi GIÁ TRỊ của các thuộc tính.
//                 Hai TeamResult bằng nhau nếu tất cả thuộc tính bằng nhau.
//                 IMMUTABLE — không thay đổi sau khi tạo.
//                 Ví dụ: Money(100, "VND"), Address("123 Abc St"), TeamResult.
//
// C# RECORD (C# 9+): Hoàn hảo để implement Value Object vì:
//   - Tự generate Equals() và GetHashCode() dựa trên tất cả properties.
//   - Immutable by default với init accessor.
//   - with-expression: var result2 = result1 with { TeamA = newTeamA };
//
// Tại sao KHÔNG trả List<List<Member>> thẳng từ TeamService?
//   - Thiếu type safety: result[0] hay result[1] là team nào?
//   - Không có computed properties (TotalSkillPoint, AverageSkillPoint...).
//   - Khó extend: Thêm metadata (thời điểm chia, algorithm dùng) cần refactor nhiều nơi.
using VolleySquad.Api.Domain;

namespace VolleySquad.Api.Domain.ValueObjects
{
    public record TeamResult
    {
        // init: Chỉ có thể set trong constructor hoặc object initializer.
        // Sau khi tạo xong, KHÔNG thể thay đổi => Immutability.
        public List<Member> TeamA { get; init; } = new();
        public List<Member> TeamB { get; init; } = new();
        public List<Member> TeamC { get; init; } = new();

        // ============================================================
        // COMPUTED PROPERTIES - Tính toán từ data hiện có
        // ============================================================
        // Không lưu DB (không có setter) — tính toán mỗi lần truy cập.
        // Tốt hơn method vì cú pháp gọi như property: result.TeamATotalSkill
        // Nếu tính toán nặng, dùng lazy backing field: private int? _total; return _total ??= ...
        public int TeamATotalSkill => TeamA.Sum(m => m.SkillPoint);
        public int TeamBTotalSkill => TeamB.Sum(m => m.SkillPoint);
        public int TeamCTotalSkill => TeamC.Sum(m => m.SkillPoint);

        // Độ lệch tối đa giữa 3 đội (số càng nhỏ = chia đội càng cân bằng)
        // Dùng để đánh giá chất lượng thuật toán chia đội
        public int MaxSkillDifference
        {
            get
            {
                var totals = new[] { TeamATotalSkill, TeamBTotalSkill, TeamCTotalSkill };
                return totals.Max() - totals.Min();
            }
        }

        public int TotalPlayers => TeamA.Count + TeamB.Count + TeamC.Count;

        // Kiểm tra nhanh nếu 3 đội cân bằng về quân số (không nhất thiết phải bằng nhau)
        public bool IsBalancedInSize =>
            Math.Abs(TeamA.Count - TeamB.Count) <= 1 &&
            Math.Abs(TeamB.Count - TeamC.Count) <= 1;

        // ============================================================
        // FACTORY METHOD - Tạo TeamResult từ danh sách 3 đội
        // ============================================================
        // Static factory method thay thế constructor phức tạp.
        // Lợi ích: Tên method mô tả rõ intent hơn new TeamResult(...).
        public static TeamResult Create(List<Member> teamA, List<Member> teamB, List<Member> teamC)
            => new() { TeamA = teamA, TeamB = teamB, TeamC = teamC };
    }
}
