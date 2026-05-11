// ============================================================
// MATCH - Domain Entity (Trận đấu bóng chuyền)
// ============================================================
// RICH DOMAIN MODEL vs ANEMIC DOMAIN MODEL - Câu hỏi phỏng vấn thường gặp:
//
//   Anemic Domain Model (ANTI-PATTERN):
//     Entity chỉ có property get/set, KHÔNG có business logic.
//     Logic bị rải rác ở Controller, Service, hoặc nhiều nơi.
//     Dẫn đến: duplicate code, khó maintain, vi phạm OOP encapsulation.
//
//   Rich Domain Model (RECOMMENDED cho domain phức tạp):
//     Entity chứa behavior và tự bảo vệ invariants (ràng buộc bất biến).
//     Ví dụ: match.TryRegisterMember() tự kiểm tra full/duplicate.
//     Không thể vi phạm business rule từ bên ngoài vì logic ở trong entity.
//
// ⚠️ MIGRATION NOTE: Thêm property Status yêu cầu tạo migration mới:
//     dotnet ef migrations add AddMatchStatus
//     dotnet ef database update
using VolleySquad.Api.Domain.Enums;
using VolleySquad.Api.Domain.Exceptions;

namespace VolleySquad.Api.Domain
{
    public class Match
    {
        // ============================================================
        // CONSTANTS - Ràng buộc bất biến (Invariants) của Domain
        // ============================================================
        // Tập trung constants trong Entity để tránh magic numbers rải rác code.
        // Nếu business quy định thay đổi (MaxSlots = 24), chỉ sửa 1 chỗ.
        public const int DefaultMaxSlots = 18;   // 3 đội × 6 người
        public const int MinSlots = 6;            // Ít nhất 2 đội × 3 người
        public const int AbsoluteMaxSlots = 30;   // Giới hạn cứng (3 đội × 10 người)

        public Guid Id { get; set; }

        // DateTime luôn lưu theo UTC trong DB để tránh vấn đề timezone.
        // Khi hiển thị cho user: chuyển sang giờ địa phương (+7 cho Việt Nam) ở frontend.
        public DateTime PlayDate { get; set; }

        public string Location { get; set; } = "Sân UTE";

        // MaxSlots: Số slot tối đa. Default 18 (3 đội × 6 người/đội).
        public int MaxSlots { get; set; } = DefaultMaxSlots;

        // List<Guid> được lưu dưới dạng JSON string trong SQL Server
        // (cấu hình trong AppDbContext.OnModelCreating bằng Value Converter).
        // Dùng = new() để đảm bảo luôn là empty list, không bao giờ là null.
        public List<Guid> RegisteredMemberIds { get; set; } = new();

        // ============================================================
        // STATUS - Trạng thái vòng đời trận đấu (State Machine)
        // ============================================================
        // EF Core lưu enum dưới dạng string (cấu hình trong AppDbContext).
        // Lý do: "Upcoming" trong DB dễ debug hơn "0".
        public MatchStatus Status { get; set; } = MatchStatus.Upcoming;

        // ============================================================
        // SETTLEMENT INFO - Thông tin sau khi Admin chốt tiền sân
        // ============================================================
        // IsSettled: Redundant với Status == Settled, giữ lại để backward-compat
        // với các query hiện tại và Payment.Worker đang dùng.
        // FeePerPerson: Tiền sân mỗi người phải trả.
        public bool IsSettled { get; set; } = false;
        public decimal FeePerPerson { get; set; } = 0;

        // ============================================================
        // NAVIGATION PROPERTIES - EF Core Relationship
        // ============================================================
        // Navigation property cho phép EF Core load dữ liệu liên quan.
        //
        // EAGER LOADING (.Include()):
        //   var match = await _context.Matches
        //       .Include(m => m.SlotTransfers)
        //           .ThenInclude(st => st.FromMember)
        //       .FirstOrDefaultAsync(m => m.Id == id);
        //   → 1 SQL query với JOIN, load tất cả cùng lúc.
        //   → Dùng khi BIẾT CHẮC cần navigation data (trang chi tiết trận).
        //
        // LAZY LOADING (virtual + UseLazyLoadingProxies):
        //   var match = await _context.Matches.FindAsync(id);
        //   var count = match.SlotTransfers.Count; // ← Tự động query DB tại đây!
        //   → Tiện lợi nhưng nguy hiểm: N+1 Problem.
        //   → Nếu loop 100 match và access .SlotTransfers của mỗi cái → 101 queries!
        //   → KHÔNG khuyến nghị dùng trong production mà không biết rõ.
        //
        // EXPLICIT LOADING (.Entry().Collection().LoadAsync()):
        //   await _context.Entry(match).Collection(m => m.SlotTransfers).LoadAsync();
        //   → Load có kiểm soát sau khi đã fetch entity.
        //   → Dùng khi cần load navigation property có điều kiện (chỉ load nếu IsSettled).
        //
        // virtual: Bắt buộc nếu muốn dùng LAZY LOADING (EF Core tạo proxy class).
        //          Optional nếu chỉ dùng EAGER hoặc EXPLICIT LOADING.
        public virtual ICollection<SlotTransfer> SlotTransfers { get; set; } = new List<SlotTransfer>();

        // ============================================================
        // DOMAIN METHODS - Business Logic nằm trong Entity
        // ============================================================
        // Mỗi method tự kiểm tra invariants và throw DomainException nếu vi phạm.
        // Controller chỉ cần: try { match.TryRegisterMember(id); } catch (DomainException ex) { return BadRequest(ex.Message); }

        /// <summary>
        /// Kiểm tra Member đã đăng ký trong trận này chưa.
        /// </summary>
        public bool HasRegistered(Guid memberId) => RegisteredMemberIds.Contains(memberId);

        /// <summary>
        /// Kiểm tra trận đã đầy slot chưa.
        /// </summary>
        public bool IsFullyBooked() => RegisteredMemberIds.Count >= MaxSlots;

        /// <summary>
        /// Số slot còn trống.
        /// </summary>
        public int AvailableSlots() => MaxSlots - RegisteredMemberIds.Count;

        /// <summary>
        /// Đăng ký Member vào trận. Tự validate tất cả điều kiện cần thiết.
        /// Throw DomainException nếu vi phạm business rule.
        /// </summary>
        /// <param name="memberId">Id của Member muốn đăng ký</param>
        public void RegisterMember(Guid memberId)
        {
            // Guard Clause pattern: xử lý error case trước, happy path ở cuối.
            // Dễ đọc hơn nested if-else.
            if (Status != MatchStatus.Upcoming)
                throw new DomainException(
                    $"Không thể đăng ký slot khi trận ở trạng thái '{Status}'. Chỉ cho phép ở trạng thái Upcoming.",
                    "MATCH_NOT_UPCOMING");

            if (IsFullyBooked())
                throw new DomainException(
                    $"Sân đã đầy {MaxSlots} slot!",
                    "MATCH_FULL");

            if (HasRegistered(memberId))
                throw new DomainException(
                    "Bạn đã đăng ký trận này rồi.",
                    "ALREADY_REGISTERED");

            RegisteredMemberIds.Add(memberId);
        }

        /// <summary>
        /// Chuyển slot từ người A sang người B.
        /// Method này gom rule của flow AcceptSlot vào Domain để controller không phải tự nhớ
        /// từng bước Remove/Add dễ sai hoặc quên validate trạng thái.
        /// </summary>
        public void TransferSlot(Guid fromMemberId, Guid toMemberId)
        {
            if (fromMemberId == toMemberId)
                throw new DomainException("Không thể chuyển slot cho chính mình.", "TRANSFER_TO_SELF");

            if (!HasRegistered(fromMemberId))
                throw new DomainException("Người nhượng không còn slot trong trận này.", "FROM_MEMBER_NOT_REGISTERED");

            if (HasRegistered(toMemberId))
                throw new DomainException("Người nhận đã có slot trong trận này.", "TO_MEMBER_ALREADY_REGISTERED");

            if (Status == MatchStatus.Cancelled)
                throw new DomainException("Không thể chuyển slot cho trận đã bị huỷ.", "MATCH_CANCELLED");

            RegisteredMemberIds.Remove(fromMemberId);
            RegisteredMemberIds.Add(toMemberId);
        }

        /// <summary>
        /// Xóa Member khỏi trận (dùng khi Pass Slot hoàn tất hoặc Admin kick).
        /// </summary>
        public void RemoveMember(Guid memberId)
        {
            if (!HasRegistered(memberId))
                throw new DomainException(
                    "Member này không có slot trong trận đấu.",
                    "NOT_REGISTERED");

            // Chỉ cho phép xóa khi trận chưa diễn ra, hoặc đang xử lý slot transfer
            if (Status == MatchStatus.Finished || Status == MatchStatus.Settled)
                throw new DomainException(
                    "Không thể xóa slot sau khi trận đã kết thúc.",
                    "MATCH_ALREADY_FINISHED");

            RegisteredMemberIds.Remove(memberId);
        }

        /// <summary>
        /// Admin chuyển trạng thái trận sang InProgress (bắt đầu thi đấu).
        /// </summary>
        public void Start()
        {
            if (Status != MatchStatus.Upcoming)
                throw new DomainException($"Chỉ có thể bắt đầu trận đang ở trạng thái Upcoming. Hiện tại: {Status}");

            if (RegisteredMemberIds.Count < MinSlots)
                throw new DomainException($"Cần ít nhất {MinSlots} người để bắt đầu trận. Hiện có: {RegisteredMemberIds.Count}");

            Status = MatchStatus.InProgress;
        }

        /// <summary>
        /// Admin kết thúc trận — trigger sự kiện Ranking.Worker xử lý SkillPoint.
        /// </summary>
        public void Finish()
        {
            if (Status != MatchStatus.InProgress)
                throw new DomainException($"Chỉ có thể kết thúc trận đang InProgress. Hiện tại: {Status}");

            Status = MatchStatus.Finished;
        }

        /// <summary>
        /// Admin chốt tiền sân — chia đều phí sân cho số người tham gia.
        /// Đây là bước cuối, sau khi Payment.Worker xử lý xong.
        /// </summary>
        /// <param name="totalCourtFee">Tổng tiền thuê sân (VNĐ)</param>
        public void Settle(decimal totalCourtFee)
        {
            if (Status != MatchStatus.Finished)
                throw new DomainException("Chỉ có thể chốt tiền sau khi trận đã kết thúc (Finished).");

            if (totalCourtFee <= 0)
                throw new DomainException("Tiền sân phải lớn hơn 0.");

            if (RegisteredMemberIds.Count == 0)
                throw new DomainException("Không có thành viên nào trong trận để chia tiền.");

            // decimal chia decimal: kết quả chính xác (không mất precision như float/double)
            FeePerPerson = totalCourtFee / RegisteredMemberIds.Count;
            IsSettled = true;
            Status = MatchStatus.Settled;
        }

        /// <summary>
        /// Admin hủy trận (có thể ở bất kỳ giai đoạn nào trước khi Settled).
        /// </summary>
        public void Cancel()
        {
            if (Status == MatchStatus.Settled)
                throw new DomainException("Không thể hủy trận đã được chốt tiền. Liên hệ Admin để xử lý thủ công.");

            if (Status == MatchStatus.Cancelled)
                throw new DomainException("Trận này đã bị hủy rồi.");

            Status = MatchStatus.Cancelled;
        }
    }
}