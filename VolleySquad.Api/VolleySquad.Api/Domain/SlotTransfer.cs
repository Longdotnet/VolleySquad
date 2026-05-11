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
//
// STATE MACHINE STATUS:
//   Pending → Completed (khi ToMember gọi AcceptSlot)
//   Pending → Cancelled (khi FromMember hoặc Admin gọi CancelSlot)
using VolleySquad.Api.Domain.Exceptions;

namespace VolleySquad.Api.Domain
{
    public class SlotTransfer
    {
        public Guid Id { get; set; }

        // Trận đấu mà slot thuộc về — Foreign Key
        public Guid MatchId { get; set; }

        // Người nhượng slot (phải đang có slot trong trận)
        public Guid FromMemberId { get; set; }

        // Người nhận slot (chưa có slot trong trận)
        public Guid ToMemberId { get; set; }

        // Trạng thái: "Pending" | "Completed" | "Cancelled"
        // INTERVIEW NOTE: Nếu muốn compile-time safety → dùng enum SlotTransferStatus.
        // Hiện giữ string để backward-compat với code cũ và query đang chạy.
        public string Status { get; set; } = "Pending";

        // UTC timestamp — frontend convert sang giờ địa phương
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Timestamp khi hoàn tất hoặc huỷ (nullable — chưa resolve thì null)
        public DateTime? ResolvedAt { get; set; }

        // ============================================================
        // NAVIGATION PROPERTIES - EF Core Relationships
        // ============================================================
        // Thuộc về 1 Match (Many SlotTransfers → 1 Match)
        // Khi EAGER LOADING:
        //   var transfer = await _context.SlotTransfers
        //       .Include(st => st.Match)
        //       .Include(st => st.FromMember)
        //       .Include(st => st.ToMember)
        //       .FirstOrDefaultAsync(st => st.Id == id);
        //   → 1 query với 3 JOIN — load tất cả trong 1 lần.
        //   → Dùng cho trang "Chi tiết yêu cầu transfer".
        //
        // null! (null-forgiving operator): Báo cho compiler biết "tôi đảm bảo
        // prop này sẽ không null khi dùng" — EF Core sẽ populate khi Include/Load.
        // Nếu không có null!, compiler báo warning CS8618 (non-nullable uninitialized).
        public virtual Match Match { get; set; } = null!;
        public virtual Member FromMember { get; set; } = null!;
        public virtual Member ToMember { get; set; } = null!;

        // ============================================================
        // DOMAIN METHODS - Chuyển trạng thái
        // ============================================================

        /// <summary>
        /// Người nhận slot (ToMember) xác nhận nhận slot.
        /// Chỉ ToMember được phép gọi method này.
        /// </summary>
        /// <param name="requestingMemberId">Id của người đang gọi — phải là ToMemberId</param>
        public void Accept(Guid requestingMemberId)
        {
            // Authorization check trong Domain: Bảo vệ invariant "chỉ ToMember mới xác nhận"
            // Tách khỏi Controller để reusable và testable.
            if (requestingMemberId != ToMemberId)
                throw new DomainException(
                    "Chỉ người được chỉ định nhận slot mới có thể xác nhận yêu cầu này.",
                    "UNAUTHORIZED_ACCEPT");

            if (Status != "Pending")
                throw new DomainException(
                    $"Yêu cầu này đã ở trạng thái '{Status}', không thể xác nhận.",
                    "TRANSFER_NOT_PENDING");

            Status = "Completed";
            ResolvedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Huỷ yêu cầu nhượng slot. Cho phép FromMember tự huỷ hoặc Admin huỷ hộ.
        /// </summary>
        /// <param name="requestingMemberId">Id của người huỷ</param>
        /// <param name="requestingRole">Role của người huỷ ("Admin" hoặc "Member")</param>
        public void Cancel(Guid requestingMemberId, string requestingRole = "Member")
        {
            // Admin có thể huỷ bất kỳ transfer nào (force cancel)
            bool isAdmin = requestingRole == "Admin";
            bool isFromMember = requestingMemberId == FromMemberId;

            if (!isAdmin && !isFromMember)
                throw new DomainException(
                    "Chỉ người tạo yêu cầu hoặc Admin mới có thể huỷ.",
                    "UNAUTHORIZED_CANCEL");

            if (Status != "Pending")
                throw new DomainException(
                    $"Yêu cầu đã ở trạng thái '{Status}', không thể huỷ.",
                    "TRANSFER_NOT_PENDING");

            Status = "Cancelled";
            ResolvedAt = DateTime.UtcNow;
        }

        /// <summary>
        /// Kiểm tra transfer có đang chờ xử lý không.
        /// </summary>
        public bool IsPending() => Status == "Pending";

        /// <summary>
        /// Kiểm tra transfer đã hoàn tất chưa.
        /// </summary>
        public bool IsCompleted() => Status == "Completed";
    }
}
