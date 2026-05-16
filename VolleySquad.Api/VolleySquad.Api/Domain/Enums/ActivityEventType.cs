// ============================================================
// ACTIVITY EVENT TYPE - Enum định nghĩa loại hoạt động
// ============================================================
// Mỗi giá trị đại diện cho 1 sự kiện có thể xảy ra trong hệ thống.
// EF Core sẽ lưu dưới dạng string (HasConversion<string>()) để dễ
// đọc khi query DB trực tiếp, thay vì số nguyên khó hiểu.
//
// CONVENTION: Tên enum viết theo kiểu PastTense (sự kiện đã xảy ra)
// để phân biệt với Command/Request (MemberJoin vs MemberJoined).
// ============================================================
namespace VolleySquad.Api.Domain.Enums
{
    public enum ActivityEventType
    {
        // ──────── Member events ────────────────────────────────────
        // Thành viên mới đăng ký vào hệ thống (Admin tạo thủ công)
        MemberRegistered = 0,

        // Thành viên đăng ký vào slot của một trận đấu
        MemberJoinedMatch = 1,

        // Thành viên rút khỏi slot (hủy đăng ký)
        MemberLeftMatch = 2,

        // Thành viên chuyển slot của mình cho thành viên khác
        SlotTransferred = 3,

        // Thành viên nhận slot từ thành viên khác
        SlotAccepted = 4,

        // Cập nhật skill point sau kết quả trận đấu
        SkillPointUpdated = 5,

        // ──────── Match events ─────────────────────────────────────
        // Admin tạo trận mới, mở đăng ký slot
        MatchCreated = 10,

        // Trận bắt đầu (Admin chuyển trạng thái InProgress)
        MatchStarted = 11,

        // Trận kết thúc, kết quả được ghi nhận
        MatchFinished = 12,

        // Admin chốt tiền sân, trừ balance thành viên
        MatchSettled = 13,

        // Trận bị huỷ (không đủ người, thời tiết...)
        MatchCancelled = 14,

        // ──────── System events ────────────────────────────────────
        // Ranking Worker xử lý xong sự kiện từ RabbitMQ
        RankingUpdated = 20,

        // Thành viên đạt mốc skill point quan trọng (25, 50, 75, 90)
        MilestoneReached = 21,

        // Trận đầy slot (đủ số người đăng ký)
        MatchFullyBooked = 22,
    }
}
