// ============================================================
// MATCH ACTIVITY - Domain Entity (Nhật ký hoạt động hệ thống)
// ============================================================
// Entity này ghi lại mọi sự kiện đáng chú ý trong hệ thống:
// thành viên tham gia trận, trận kết thúc, skill point thay đổi...
//
// THIẾT KẾ: Append-only log (chỉ thêm, không sửa/xóa)
//   - Immutable sau khi tạo (không có setter public nào ngoài EF)
//   - RelatedMemberId / RelatedMatchId là nullable Guid:
//       Sự kiện hệ thống (RankingUpdated) có thể không liên quan member/match cụ thể.
//
// TƯƠNG QUAN VỚI DOMAIN EVENT:
//   Trong DDD thuần: Domain Event được publish khi aggregate thay đổi.
//   Ở đây ta dùng cách đơn giản hơn: Service trực tiếp ghi vào bảng này.
//   Tradeoff: Đơn giản hơn, nhưng gắn chặt với DB (không async, không fan-out).
//
// INDEX STRATEGY:
//   - Index trên OccurredAt (DESC) để query "20 hoạt động gần nhất" nhanh
//   - Index trên EventType để filter theo loại sự kiện
//   - Index trên RelatedMemberId để xem lịch sử của 1 thành viên
// ============================================================
using VolleySquad.Api.Domain.Enums;

namespace VolleySquad.Api.Domain
{
    public class MatchActivity
    {
        // Primary Key — Guid để hỗ trợ insert từ nhiều nguồn không cần auto-increment
        public Guid Id { get; set; }

        // Loại sự kiện — lưu dưới dạng string trong DB (xem cấu hình AppDbContext)
        public ActivityEventType EventType { get; set; }

        // Mô tả ngắn gọn, dễ đọc (VD: "Trần Minh Tuấn đăng ký slot trận #42")
        // Không chứa thông tin nhạy cảm. Hiển thị thẳng ra UI.
        public string Message { get; set; } = string.Empty;

        // FK tùy chọn đến Member liên quan (nullable)
        // EF Core ánh xạ nullable Guid? → NULL trong DB
        public Guid? RelatedMemberId { get; set; }

        // FK tùy chọn đến Match liên quan (nullable)
        public Guid? RelatedMatchId { get; set; }

        // Thời điểm sự kiện xảy ra, lưu UTC
        // Khi hiển thị: frontend cộng +7 hoặc dùng toLocaleString()
        public DateTime OccurredAt { get; set; }

        // ============================================================
        // FACTORY METHOD - Đảm bảo MatchActivity luôn hợp lệ khi tạo
        // ============================================================
        // Thay vì để caller tự set từng property (dễ quên OccurredAt),
        // dùng static factory method để tạo object đúng cách.
        // "Rich Domain" approach: logic tạo object nằm trong entity.
        // ============================================================

        /// <summary>
        /// Tạo activity khi một thành viên đăng ký slot vào trận.
        /// </summary>
        public static MatchActivity MemberJoinedMatch(Guid memberId, Guid matchId, string memberName, int matchNumber)
            => new()
            {
                Id              = Guid.NewGuid(),
                EventType       = ActivityEventType.MemberJoinedMatch,
                Message         = $"{memberName} đăng ký tham gia trận #{matchNumber}",
                RelatedMemberId = memberId,
                RelatedMatchId  = matchId,
                OccurredAt      = DateTime.UtcNow,
            };

        /// <summary>
        /// Tạo activity khi trận kết thúc với tỷ số.
        /// </summary>
        public static MatchActivity MatchFinished(Guid matchId, int matchNumber, string scoreTeamA, string scoreTeamB)
            => new()
            {
                Id             = Guid.NewGuid(),
                EventType      = ActivityEventType.MatchFinished,
                Message        = $"Trận #{matchNumber} kết thúc — Đội A {scoreTeamA} : {scoreTeamB} Đội B",
                RelatedMatchId = matchId,
                OccurredAt     = DateTime.UtcNow,
            };

        /// <summary>
        /// Tạo activity khi Admin tạo trận mới.
        /// </summary>
        public static MatchActivity MatchCreated(Guid matchId, int matchNumber, DateTime playDate, string location)
            => new()
            {
                Id             = Guid.NewGuid(),
                EventType      = ActivityEventType.MatchCreated,
                Message        = $"Admin tạo trận #{matchNumber} — {playDate:dd/MM} tại {location}",
                RelatedMatchId = matchId,
                OccurredAt     = DateTime.UtcNow,
            };

        /// <summary>
        /// Tạo activity khi thành viên mới được đăng ký vào hệ thống.
        /// </summary>
        public static MatchActivity MemberRegistered(Guid memberId, string memberName, int skillPoint)
            => new()
            {
                Id              = Guid.NewGuid(),
                EventType       = ActivityEventType.MemberRegistered,
                Message         = $"{memberName} gia nhập VolleySquad (skill: {skillPoint}sp)",
                RelatedMemberId = memberId,
                OccurredAt      = DateTime.UtcNow,
            };

        /// <summary>
        /// Tạo activity khi skill point của thành viên được cập nhật.
        /// </summary>
        public static MatchActivity SkillPointUpdated(Guid memberId, string memberName, int oldSp, int newSp)
        {
            var diff = newSp - oldSp;
            var direction = diff >= 0 ? $"+{diff}" : $"{diff}";
            return new()
            {
                Id              = Guid.NewGuid(),
                EventType       = ActivityEventType.SkillPointUpdated,
                Message         = $"{memberName} cập nhật skill: {oldSp}sp → {newSp}sp ({direction})",
                RelatedMemberId = memberId,
                OccurredAt      = DateTime.UtcNow,
            };
        }

        /// <summary>
        /// Tạo activity khi trận đầy slot.
        /// </summary>
        public static MatchActivity MatchFullyBooked(Guid matchId, int matchNumber, int slots)
            => new()
            {
                Id             = Guid.NewGuid(),
                EventType      = ActivityEventType.MatchFullyBooked,
                Message        = $"Trận #{matchNumber} — {slots}/{slots} slot đã lấp đầy",
                RelatedMatchId = matchId,
                OccurredAt     = DateTime.UtcNow,
            };

        /// <summary>
        /// Tạo activity khi thành viên đạt mốc skill.
        /// </summary>
        public static MatchActivity MilestoneReached(Guid memberId, string memberName, int milestone)
        {
            var tier = milestone switch
            {
                >= 90 => "Gold",
                >= 70 => "Silver",
                >= 50 => "Bronze",
                _     => "Iron",
            };
            return new()
            {
                Id              = Guid.NewGuid(),
                EventType       = ActivityEventType.MilestoneReached,
                Message         = $"{memberName} lên hạng {tier} ({milestone}sp)",
                RelatedMemberId = memberId,
                OccurredAt      = DateTime.UtcNow,
            };
        }

        /// <summary>
        /// Tạo activity khi Admin chốt tiền sân.
        /// </summary>
        public static MatchActivity MatchSettled(Guid matchId, int matchNumber, decimal feePerPerson, int playerCount)
            => new()
            {
                Id             = Guid.NewGuid(),
                EventType      = ActivityEventType.MatchSettled,
                Message        = $"Trận #{matchNumber} chốt tiền — {playerCount} người × {feePerPerson:N0}đ",
                RelatedMatchId = matchId,
                OccurredAt     = DateTime.UtcNow,
            };

        /// <summary>
        /// Tạo activity khi Ranking Worker xử lý xong sự kiện.
        /// </summary>
        public static MatchActivity RankingUpdated(Guid matchId, int matchNumber)
            => new()
            {
                Id             = Guid.NewGuid(),
                EventType      = ActivityEventType.RankingUpdated,
                Message        = $"Ranking Worker cập nhật điểm sau trận #{matchNumber} (RabbitMQ event)",
                RelatedMatchId = matchId,
                OccurredAt     = DateTime.UtcNow,
            };
    }
}
