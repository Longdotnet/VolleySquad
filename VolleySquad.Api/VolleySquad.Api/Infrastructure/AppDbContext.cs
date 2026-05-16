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
using Microsoft.EntityFrameworkCore.ChangeTracking;
using VolleySquad.Api.Domain;
using VolleySquad.Api.Domain.Enums;

namespace VolleySquad.Api.Infrastructure
{
    // DBContext đóng vai trò như một đơn vị làm việc (Unit of Work)
    public class AppDbContext : DbContext
    {
        // base(options): Chuyển connection string và EF config lên lớp cha DbContext.
        // DbContextOptions được DI Container inject từ Program.cs (AddDbContext).
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        // DbSet<T>: Đại diện cho bảng trong DB. Dùng để query, add, remove entities.
        // EF Core dùng tên property này làm tên bảng (Members, Matches, SlotTransfers).
        public DbSet<Member> Members { get; set; }
        public DbSet<Match> Matches { get; set; }
        public DbSet<SlotTransfer> SlotTransfers { get; set; }

        // MatchActivities: Bảng nhật ký hoạt động hệ thống (append-only).
        // Được dùng bởi PublicController để hiển thị feed hoạt động trang login
        // và trang dashboard (không cần auth để đọc).
        public DbSet<MatchActivity> MatchActivities { get; set; }

        // ============================================================
        // ONMODELCREATING - Cấu hình EF Core với Fluent API
        // ============================================================
        // Fluent API được ưu tiên hơn Data Annotations ([Column], [MaxLength]...)
        // vì giữ Domain model "sạch" — không phụ thuộc EF Core attributes.
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ============================================================
            // MATCH - Cấu hình bảng Matches
            // ============================================================
            modelBuilder.Entity<Match>(entity =>
            {
                // Value Converter: List<Guid> → JSON string trong DB
                // Vấn đề: SQL Server không có kiểu "array".
                // Giải pháp: Serialize sang JSON string khi LƯU, Deserialize khi ĐỌC.
                var registeredMemberIdsProperty = entity.Property(m => m.RegisteredMemberIds)
                    .HasConversion(
                        ids => System.Text.Json.JsonSerializer.Serialize(ids, (System.Text.Json.JsonSerializerOptions?)null),
                        json => System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json, (System.Text.Json.JsonSerializerOptions?)null) ?? new List<Guid>()
                    )
                    .HasColumnType("nvarchar(max)");

                registeredMemberIdsProperty
                    // ValueComparer giúp EF Core biết cách so sánh List<Guid> theo "nội dung"
                    // thay vì chỉ so sánh reference của object list.
                    // Nếu thiếu comparer, EF có thể không detect thay đổi khi mutate list in-place.
                    .Metadata.SetValueComparer(new ValueComparer<List<Guid>>(
                        (left, right) => left!.SequenceEqual(right!),
                        ids => ids.Aggregate(0, (current, id) => HashCode.Combine(current, id.GetHashCode())),
                        ids => ids.ToList()));

                // Enum → String Converter: Lưu "Upcoming" thay vì 0 trong DB.
                // Dễ debug khi query DB trực tiếp. Tốn thêm vài bytes/row nhưng đáng.
                // Nếu muốn lưu số nguyên (compact hơn): dùng .HasConversion<int>()
                entity.Property(m => m.Status)
                    .HasConversion<string>()
                    .HasMaxLength(20)
                    .HasDefaultValue(MatchStatus.Upcoming);

                // decimal(18,2) tránh EF dùng precision mặc định không rõ ràng.
                // Interview note: tiền tệ luôn nên cấu hình precision tường minh ở DB layer.
                entity.Property(m => m.FeePerPerson)
                    .HasColumnType("decimal(18,2)");

                // ============================================================
                // NAVIGATION PROPERTY: Match → SlotTransfers (One-to-Many)
                // ============================================================
                // Cấu hình tường minh quan hệ 1 Match có nhiều SlotTransfer.
                // EF Core có thể tự detect (convention-based) nhưng explicit tốt hơn
                // để kiểm soát cascade delete behavior.
                //
                // OnDelete: Restrict = Khi xóa Match, báo lỗi nếu còn SlotTransfers.
                // Thay vì Cascade (tự xóa SlotTransfers theo) → tránh mất dữ liệu ngoài ý muốn.
                entity.HasMany(m => m.SlotTransfers)
                    .WithOne(st => st.Match)
                    .HasForeignKey(st => st.MatchId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // ============================================================
            // MEMBER - Cấu hình bảng Members
            // ============================================================
            modelBuilder.Entity<Member>(entity =>
            {
                // decimal(18,2): 18 chữ số tổng, 2 chữ số thập phân (đủ cho VNĐ)
                entity.Property(m => m.Balance)
                    .HasColumnType("decimal(18,2)");

                // ============================================================
                // NAVIGATION: Member → OutgoingTransfers (một Member có thể nhượng nhiều lần)
                // ============================================================
                // HasForeignKey: EF Core biết SlotTransfer.FromMemberId là FK trỏ đến Member.Id
                // NoAction: Không cascade delete — giữ lại lịch sử transfer dù member bị xóa.
                entity.HasMany(m => m.OutgoingTransfers)
                    .WithOne(st => st.FromMember)
                    .HasForeignKey(st => st.FromMemberId)
                    .OnDelete(DeleteBehavior.NoAction);

                // NAVIGATION: Member → IncomingTransfers
                // Cùng bảng SlotTransfers nhưng FK khác (ToMemberId)
                entity.HasMany(m => m.IncomingTransfers)
                    .WithOne(st => st.ToMember)
                    .HasForeignKey(st => st.ToMemberId)
                    .OnDelete(DeleteBehavior.NoAction);
            });

            // ============================================================
            // SLOT TRANSFER - Cấu hình bảng SlotTransfers
            // ============================================================
            modelBuilder.Entity<SlotTransfer>(entity =>
            {
                // Index trên Status để query "SELECT * WHERE Status = 'Pending'" nhanh hơn.
                // Không index: Full table scan O(n).
                // Có index: B-tree lookup O(log n).
                entity.HasIndex(st => st.Status)
                    .HasDatabaseName("IX_SlotTransfers_Status");

                // Composite index: Query theo MatchId + Status (dùng trong PassSlot validation)
                // "Tìm tất cả Pending transfer của Match X" → rất phổ biến.
                entity.HasIndex(st => new { st.MatchId, st.Status })
                    .HasDatabaseName("IX_SlotTransfers_MatchId_Status");
            });

            // ============================================================
            // MATCH ACTIVITY - Cấu hình bảng MatchActivities
            // ============================================================
            modelBuilder.Entity<MatchActivity>(entity =>
            {
                // Enum → String cho EventType: "MemberJoinedMatch" dễ đọc hơn "1"
                entity.Property(a => a.EventType)
                    .HasConversion<string>()
                    .HasMaxLength(40);

                // Message không nên quá dài (tối đa 500 ký tự là đủ cho feed)
                entity.Property(a => a.Message)
                    .HasMaxLength(500)
                    .IsRequired();

                // Index trên OccurredAt DESC cho query "20 hoạt động gần nhất"
                // Query phổ biến nhất: SELECT TOP 20 ... ORDER BY OccurredAt DESC
                // Nếu không có index: full table scan mỗi lần tải trang login.
                entity.HasIndex(a => a.OccurredAt)
                    .HasDatabaseName("IX_MatchActivities_OccurredAt")
                    .IsDescending(true);

                // Index trên EventType để filter theo loại sự kiện
                entity.HasIndex(a => a.EventType)
                    .HasDatabaseName("IX_MatchActivities_EventType");

                // Index trên RelatedMemberId để xem lịch sử của 1 thành viên cụ thể
                entity.HasIndex(a => a.RelatedMemberId)
                    .HasDatabaseName("IX_MatchActivities_RelatedMemberId");
            });
        }
    }

    // ============================================================
    // EAGER LOADING vs LAZY LOADING vs EXPLICIT LOADING
    // ============================================================
    // Đây là phần lý thuyết quan trọng để phỏng vấn, không phải code thực:
    //
    // ─── EAGER LOADING (.Include()) ───────────────────────────────────
    // var match = await _context.Matches
    //     .Include(m => m.SlotTransfers)              // JOIN SlotTransfers
    //         .ThenInclude(st => st.FromMember)       // JOIN Members (as FromMember)
    //         .ThenInclude(st => st.ToMember)         // JOIN Members (as ToMember)
    //     .FirstOrDefaultAsync(m => m.Id == id);
    //
    // → 1 SQL query với multiple JOINs.
    // → Dùng khi BIẾT TRƯỚC cần navigation data.
    // → Tránh N+1 Problem.
    // → Có thể nặng nếu JOIN quá nhiều bảng (SELECT N columns).
    //
    // ─── LAZY LOADING (virtual + proxy) ──────────────────────────────
    // Cần: 1) builder.Services.AddDbContext<AppDbContext>(opt => opt.UseLazyLoadingProxies())
    //      2) Install Microsoft.EntityFrameworkCore.Proxies
    //      3) Navigation properties phải có modifier "virtual"
    //
    // var match = await _context.Matches.FindAsync(id); // chỉ load Match
    // var count = match.SlotTransfers.Count;            // ← Query DB TỰ ĐỘNG tại đây!
    //
    // → Tiện lợi: Không cần .Include() ở mọi nơi.
    // → ⚠️ N+1 PROBLEM: Nếu load 100 matches trong vòng loop và access .SlotTransfers:
    //   SELECT * FROM Matches           -- 1 query
    //   SELECT * FROM SlotTransfers WHERE MatchId = 'id1'  -- query 2
    //   SELECT * FROM SlotTransfers WHERE MatchId = 'id2'  -- query 3
    //   ...                             -- 100 queries nữa!
    //   Tổng: 101 queries thay vì 1! → Performance thảm họa.
    // → KHÔNG dùng lazy loading trong loop hoặc API trả list.
    //
    // ─── EXPLICIT LOADING (.Entry().Collection().LoadAsync()) ─────────
    // var match = await _context.Matches.FindAsync(id);
    //
    // // Chỉ load nếu cần thiết (conditional):
    // if (needTransferDetails)
    //     await _context.Entry(match)
    //         .Collection(m => m.SlotTransfers)
    //         .LoadAsync();
    //
    // → Kiểm soát hoàn toàn khi nào load navigation data.
    // → Tốt khi load có điều kiện (không phải lúc nào cũng cần).
    // → Phức tạp hơn Eager Loading.
    //
    // ─── KHI NÀO DÙNG GÌ? ────────────────────────────────────────────
    //   Eager:    Biết chắc cần navigation data, endpoint detail (1 record).
    //   Lazy:     Prototyping/simple apps. TRÁNH trong production API loop.
    //   Explicit: Load có điều kiện, cần kiểm soát chặt performance.
    //   AsNoTracking: Luôn dùng cho READ-ONLY query (GetAll, Leaderboard...).
}
