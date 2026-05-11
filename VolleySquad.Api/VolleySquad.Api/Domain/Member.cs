// ============================================================
// MEMBER - Domain Entity (Tầng Domain trong Clean Architecture)
// ============================================================
// Domain Entity là trung tâm của bài toán, không phụ thuộc
// vào EF Core, ASP.NET Core, hay bất kỳ framework nào.
// Nếu domain thay đổi (thêm/xóa field), chỉ sửa file này và tạo migration mới.
using VolleySquad.Api.Domain.Exceptions;

namespace VolleySquad.Api.Domain
{
    public class Member
    {
        // ============================================================
        // CONSTANTS - Giới hạn nghiệp vụ tập trung 1 chỗ
        // ============================================================
        public const int MinSkillPoint = 1;
        public const int MaxSkillPoint = 100;
        public const decimal MinBalance = 0m;   // Không cho phép số dư âm

        // Guid (Globally Unique Identifier): 128-bit, xác suất trùng gần 0.
        // Dùng Guid thay int/long làm PK vì:
        //   + Không lộ số lượng record (user không biết có bao nhiêu member)
        //   + Dễ merge/replicate dữ liệu giữa nhiều DB
        //   + Server tạo Id trước khi gửi đến DB (không phụ thuộc auto-increment của DB)
        //   - Nhược: Lớn hơn int, index không sequential => fragmentation hơn
        public Guid Id { get; set; }

        // string.Empty thay vì null làm default để tránh NullReferenceException
        public string Name { get; set; } = string.Empty;

        // Điểm kỹ năng dùng để thuật toán Snake Draft chia đội cân bằng.
        // Giá trị hợp lệ: 1-100 (bảo vệ qua AwardSkillPoints/PenalizeSkillPoints)
        public int SkillPoint { get; set; }

        // decimal thay vì float/double cho tiền tệ:
        // float/double lưu theo IEEE 754 nhị phân => 0.1 + 0.2 = 0.30000000000000004
        // decimal lưu theo thập phân chính xác => 0.1 + 0.2 = 0.3 (đúng)
        public decimal Balance { get; set; }

        // Role lưu dưới dạng string để JWT Claim có thể đọc trực tiếp.
        // Production app nên dùng enum và có converter, hoặc lưu riêng trong bảng Roles.
        public string Role { get; set; } = "Member"; // "Admin" hoặc "Member"

        // ============================================================
        // NAVIGATION PROPERTIES - EF Core Relationship
        // ============================================================
        // Member có thể là người nhượng (OutgoingTransfers) hoặc người nhận (IncomingTransfers).
        // virtual: Cho phép Lazy Loading nếu UseLazyLoadingProxies() được bật.
        //          Không bật lazy loading → virtual chỉ là marker, không có effect.
        //
        // EXPLICIT LOADING example (khi không muốn Lazy/Eager):
        //   var member = await _context.Members.FindAsync(id);
        //   // Chỉ load khi cần thiết (conditional loading):
        //   if (needTransferHistory)
        //       await _context.Entry(member).Collection(m => m.OutgoingTransfers).LoadAsync();
        public virtual ICollection<SlotTransfer> OutgoingTransfers { get; set; } = new List<SlotTransfer>();
        public virtual ICollection<SlotTransfer> IncomingTransfers { get; set; } = new List<SlotTransfer>();

        // ============================================================
        // DOMAIN METHODS - Quản lý số dư (Balance)
        // ============================================================

        /// <summary>
        /// Kiểm tra Member có đủ số dư để thanh toán không.
        /// </summary>
        public bool HasSufficientBalance(decimal amount) => Balance >= amount;

        /// <summary>
        /// Nạp tiền vào tài khoản. Amount phải dương.
        /// </summary>
        public void Deposit(decimal amount)
        {
            if (amount <= 0)
                throw new DomainException($"Số tiền nạp phải lớn hơn 0. Nhận được: {amount:N0} VNĐ");

            Balance += amount;
        }

        /// <summary>
        /// Trừ tiền từ tài khoản. Throw DomainException nếu không đủ số dư.
        /// Đảm bảo Balance KHÔNG BAO GIỜ âm (invariant).
        /// </summary>
        /// <param name="amount">Số tiền cần trừ</param>
        public void Deduct(decimal amount)
        {
            if (amount <= 0)
                throw new DomainException($"Số tiền trừ phải lớn hơn 0. Nhận được: {amount:N0} VNĐ");

            if (!HasSufficientBalance(amount))
                throw new DomainException(
                    $"Số dư không đủ. Cần {amount:N0} VNĐ, hiện có {Balance:N0} VNĐ.",
                    "INSUFFICIENT_BALANCE");

            Balance -= amount;
        }

        /// <summary>
        /// Hoàn tiền (refund) — dùng khi Pass Slot sau khi trận đã Settled.
        /// Về cơ bản là Deposit, nhưng tên method thể hiện rõ business intent hơn.
        /// </summary>
        public void Refund(decimal amount)
        {
            if (amount <= 0)
                throw new DomainException($"Số tiền hoàn phải lớn hơn 0. Nhận được: {amount:N0} VNĐ");

            Balance += amount;
        }

        // ============================================================
        // DOMAIN METHODS - Quản lý điểm kỹ năng (SkillPoint)
        // ============================================================
        // Math.Clamp(value, min, max): Giới hạn giá trị trong khoảng [min, max].
        // Tương đương: Math.Max(min, Math.Min(max, value))
        // Dùng thay vì if-else để code gọn hơn và intent rõ ràng hơn.

        /// <summary>
        /// Cộng điểm kỹ năng khi thắng. Không vượt quá MaxSkillPoint (100).
        /// </summary>
        /// <param name="points">Số điểm cộng thêm (dương)</param>
        public void AwardSkillPoints(int points)
        {
            if (points <= 0)
                throw new DomainException($"Số điểm thưởng phải dương. Nhận được: {points}");

            SkillPoint = Math.Clamp(SkillPoint + points, MinSkillPoint, MaxSkillPoint);
        }

        /// <summary>
        /// Trừ điểm kỹ năng khi thua. Không thấp hơn MinSkillPoint (1).
        /// </summary>
        /// <param name="points">Số điểm trừ (dương — hàm tự hiểu là "trừ")</param>
        public void PenalizeSkillPoints(int points)
        {
            if (points <= 0)
                throw new DomainException($"Số điểm phạt phải dương. Nhận được: {points}");

            SkillPoint = Math.Clamp(SkillPoint - points, MinSkillPoint, MaxSkillPoint);
        }

        /// <summary>
        /// Áp dụng kết quả trận: cộng hoặc trừ điểm tùy isWinner.
        /// Tập trung logic ranking vào đây thay vì để trong Ranking.Worker.
        /// Ranking.Worker chỉ cần gọi: member.ApplyMatchResult(isWinner, winPoints, losePoints)
        /// </summary>
        public void ApplyMatchResult(bool isWinner, int winPoints = 5, int losePoints = 3)
        {
            if (isWinner)
                AwardSkillPoints(winPoints);
            else
                PenalizeSkillPoints(losePoints);
        }

        /// <summary>
        /// Kiểm tra Member có phải Admin không.
        /// Utility method tránh magic string "Admin" rải rác code.
        /// </summary>
        public bool IsAdmin() => Role == "Admin";
    }
}