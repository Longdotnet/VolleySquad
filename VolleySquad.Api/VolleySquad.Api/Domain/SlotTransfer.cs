// ============================================================
// SLOT TRANSFER - Domain Entity (Yêu cầu nhượng lại slot)
// ============================================================
// Flow: Member A (có slot) tạo yêu cầu nhượng cho Member B (Pending)
//       → Member B xác nhận (Completed) hoặc A/Admin huỷ (Cancelled)
//
// Khi Completed:
//   - A bị xóa khỏi RegisteredMemberIds, B được thêm vào
//   - Nếu trận đã được chốt tiền (IsSettled = true):
//       + Hoàn lại FeePerPerson cho A
//       + Trừ FeePerPerson của B
namespace VolleySquad.Api.Domain
{
    public class SlotTransfer
    {
        public Guid Id { get; set; }

        // Trận đấu mà slot thuộc về
        public Guid MatchId { get; set; }

        // Người nhượng slot (phải đang có slot trong trận)
        public Guid FromMemberId { get; set; }

        // Người nhận slot (chưa có slot trong trận)
        public Guid ToMemberId { get; set; }

        // Trạng thái: "Pending" | "Completed" | "Cancelled"
        public string Status { get; set; } = "Pending";

        // UTC timestamp — frontend convert sang giờ địa phương
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Timestamp khi hoàn tất hoặc huỷ
        public DateTime? ResolvedAt { get; set; }
    }
}
