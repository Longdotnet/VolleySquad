// ============================================================
// MATCH STATUS - Domain Enum (Trạng thái vòng đời của trận đấu)
// ============================================================
// Tại sao dùng ENUM thay vì string magic values ("Upcoming", "Settled"...)?
//   1. Compile-time safety: Không thể typo "Upcomng" — compiler báo lỗi ngay.
//   2. Intellisense: IDE gợi ý tất cả giá trị hợp lệ.
//   3. Switch exhaustiveness: Compiler cảnh báo nếu switch thiếu case.
//   4. Refactor-safe: Đổi tên enum → tất cả chỗ dùng tự cập nhật (không phải tìm string).
//
// EF Core lưu enum theo 2 cách:
//   - Mặc định: lưu số nguyên (0, 1, 2...) — gọn nhưng khó đọc trong DB.
//   - HasConversion<string>(): lưu tên string — dễ đọc trong DB, khuyến nghị dùng.
//   (Xem cấu hình trong AppDbContext.OnModelCreating)
//
// STATE MACHINE: Các trạng thái hợp lệ và chuyển đổi được phép:
//   Upcoming → InProgress → Finished → Settled
//        ↘              ↘         ↘
//        Cancelled      Cancelled  (không cho cancel sau Finished)
//
// Tham khảo phỏng vấn: "State Pattern" — nếu logic phức tạp hơn, có thể
// tách mỗi state thành một class riêng với method chuyển trạng thái.
namespace VolleySquad.Api.Domain.Enums
{
    public enum MatchStatus
    {
        // Trận chưa diễn ra, đang mở đăng ký slot.
        // Cho phép: RegisterSlot, PassSlot, AcceptSlot, Cancel.
        Upcoming = 0,

        // Trận đang diễn ra (Admin bấm bắt đầu).
        // Không cho phép đăng ký thêm slot hoặc chuyển slot mới.
        // Cho phép: Finish.
        InProgress = 1,

        // Trận đã kết thúc, kết quả đã ghi nhận.
        // Ranking.Worker đang/đã xử lý cập nhật SkillPoint.
        // Chờ Admin chốt tiền sân.
        Finished = 2,

        // Admin đã chốt tiền sân (FinalizeMatch chạy xong).
        // Payment.Worker đã trừ tiền mọi người.
        // FeePerPerson đã được xác định.
        Settled = 3,

        // Admin huỷ trận (force cancel). 
        // Mọi yêu cầu SlotTransfer đang Pending sẽ bị huỷ theo.
        Cancelled = 4
    }
}
