// ============================================================
// APP DB CONTEXT - Cầu nối giữa C# objects và SQL Server (ORM)
// ============================================================
// ORM (Object-Relational Mapper): Ánh xạ class C# <-> bảng SQL tự động.
// Thay vì viết: "SELECT * FROM Members WHERE Id = @id"
// Ta viết:      _context.Members.FindAsync(id)
//
// DbContext đóng vai trò:
//   1. UNIT OF WORK: Gom nhiều thay đổi, SaveChanges() gửi 1 batch lên DB
//   2. IDENTITY MAP:  Cache entity trong 1 request, tránh query cùng Id 2 lần
//   3. CHANGE TRACKER: Auto detect thay đổi trên entity => sinh UPDATE statement
//
// LIFETIME: AddScoped (đăng ký ở Program.cs) — 1 instance mỗi HTTP request.
//   KHÔNG dùng Singleton: DbContext không thread-safe.
//   KHÔNG dùng Transient: Mất lợi ích Unit of Work (nhiều instance = không gom được).
using Microsoft.EntityFrameworkCore;
using VolleySquad.Api.Domain;

namespace VolleySquad.Api.Infrastructure
{
    // DBContext đóng vai trò như một đơn vị làm việc (Unit of Work)
    public class AppDbContext : DbContext
    {
        // base(options): Chuyển connection string và EF config lên lớp cha DbContext.
        // DbContextOptions được DI Container inject từ Program.cs (AddDbContext).
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSet<T>: Đại diện cho bảng trong DB. Dùng để query, add, remove entities.
        // EF Core dùng tên property này làm tên bảng (Members, Matches).
        public DbSet<Member> Members { get; set; }
        public DbSet<Match> Matches { get; set; }
        public DbSet<SlotTransfer> SlotTransfers { get; set; }

        // ============================================================
        // FIX: OnModelCreating - Cấu hình rõ ràng cách EF Core map entities
        // ============================================================
        // Fluent API ở đây được ưu tiên hơn Data Annotations ([Column], [MaxLength]...)
        // vì giữ Domain model "sạch" — không phụ thuộc EF Core attributes.
        //
        // Vấn đề với List<Guid>:
        //   SQL Server không có kiểu "array". EF Core cần Value Converter để
        //   serialize List<Guid> thành JSON string khi ghi DB và ngược lại khi đọc.
        //   Nếu không cấu hình tường minh, EF Core có thể dùng convention không rõ ràng
        //   => dễ gây lỗi sau khi upgrade EF Core version.
        //
        // VALUE CONVERTER hoạt động như sau:
        //   C# List<Guid> --(Serialize)-->  "[\"guid1\",\"guid2\"]"  --> nvarchar(max) trong DB
        //   DB string     --(Deserialize)--> C# List<Guid>           --> EF Core trả về app
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Cấu hình JSON conversion cho RegisteredMemberIds
            modelBuilder.Entity<Match>()
                .Property(m => m.RegisteredMemberIds)
                .HasConversion(
                    // Khi LƯU vào DB: List<Guid> => JSON string
                    ids => System.Text.Json.JsonSerializer.Serialize(ids, (System.Text.Json.JsonSerializerOptions?)null),
                    // Khi ĐỌC từ DB: JSON string => List<Guid>
                    json => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Guid>()
                )
                .HasColumnType("nvarchar(max)"); // Khớp với migration InitialCreate đã tạo

            // Cấu hình tường minh precision cho Balance
            // decimal(18,2): 18 chữ số tổng, 2 chữ số thập phân (đủ cho VNĐ)
            modelBuilder.Entity<Member>()
                .Property(m => m.Balance)
                .HasColumnType("decimal(18,2)");
        }
    }
}