// ============================================================
// MEMBER - Domain Entity (Tầng Domain trong Clean Architecture)
// ============================================================
// Domain Entity là trung tâm của bài toán, không phụ thuộc
// vào EF Core, ASP.NET Core, hay bất kỳ framework nào.
// Nếu domain thay đổi (thêm/xóa field), chỉ sửa file này và tạo migration mới.
namespace VolleySquad.Api.Domain
{
    public class Member
    {
        // Guid (Globally Unique Identifier): 128-bit, xảc suất trùng gần 0.
        // Dùng Guid thay int/long làm PK vì:
        //   + Không lộ số lượng record (user không biết có bao nhiêu member)
        //   + Dễ merge/replicate dữ liệu giữa nhiều DB
        //   + Server tạo Id trước khi gửi đến DB (không phụ thuộc auto-increment của DB)
        //   - Nhược: Lớn hơn int, index không sequential => fragmentation hơn
        public Guid Id { get; set; }

        // string.Empty thay vì null làm default để tránh NullReferenceException
        public string Name { get; set; } = string.Empty;

        // Điểm kỹ năng dùng để thuật toán Snake Draft chia đội cân bằng
        public int SkillPoint { get; set; } // Giá trị hợp lệ: 1-100

        // decimal thay vì float/double cho tiền tệ:
        // float/double lưu theo IEEE 754 nhị phân => 0.1 + 0.2 = 0.30000000000000004
        // decimal lưu theo thập phân chính xác => 0.1 + 0.2 = 0.3 (đúng)
        public decimal Balance { get; set; }

        // Role lưu dưới dạng string để JWT Claim có thể đọc trực tiếp.
        // Production app nên dùng enum và có converter, hoặc lưu riêng trong bảng Roles.
        public string Role { get; set; } = "Member"; // "Admin" hoặc "Member"
    }
}