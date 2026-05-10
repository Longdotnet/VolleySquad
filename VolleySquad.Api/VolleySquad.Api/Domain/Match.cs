// ============================================================
// MATCH - Domain Entity (Trận đấu bóng chuyền)
// ============================================================
namespace VolleySquad.Api.Domain
{
    public class Match
    {
        public Guid Id { get; set; }

        // DateTime luôn lưu theo UTC trong DB để tránh vấn đề timezone.
        // Khi hiển thị cho user: chuyển sang giờ địa phương (+7 cho Việt Nam) ở frontend.
        public DateTime PlayDate { get; set; }

        public string Location { get; set; } = "Sân UTE";

        // MaxSlots: Số slot tối đa. Default 18 (3 đội × 6 người/đội).
        public int MaxSlots { get; set; } = 18;

        // List<Guid> được lưu dưới dạng JSON string trong SQL Server
        // (cấu hình trong AppDbContext.OnModelCreating bằng Value Converter).
        // Dùng = new() để đảm bảo luôn là empty list, không bao giờ là null.
        public List<Guid> RegisteredMemberIds { get; set; } = new();

        // ============================================================
        // SETTLEMENT INFO - Thông tin sau khi Admin chốt tiền sân
        // ============================================================
        // IsSettled: true sau khi FinalizeMatch chạy thành công.
        // FeePerPerson: Tiền sân mỗi người phải trả (dùng để hoàn/trừ khi pass slot sau settle).
        public bool IsSettled { get; set; } = false;
        public decimal FeePerPerson { get; set; } = 0;
    }
}