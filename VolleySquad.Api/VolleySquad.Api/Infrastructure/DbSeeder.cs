// ============================================================
// DB SEEDER - Tạo dữ liệu mẫu thực cho VolleySquadDB
// ============================================================
// MỤC ĐÍCH:
//   Seed 1 lần khi DB trống để có dữ liệu thực cho:
//   1. Trang Login (stats + activity feed — không cần auth)
//   2. Dashboard (leaderboard, danh sách trận)
//   3. Demo cho người xem project
//
// THIẾT KẾ IDEMPOTENT:
//   Check Members.Any() trước khi seed → An toàn khi restart app nhiều lần.
//   Không seed lại nếu đã có data → Không làm hỏng dữ liệu đã có.
//
// DỮ LIỆU:
//   - 1 Admin + 19 Members (tên tiếng Việt, skill 25-90sp)
//   - 35 Matches (30 quá khứ Finished/Settled + 5 tương lai Upcoming)
//   - ~200+ MatchActivity records (MemberRegistered, MatchCreated, MemberJoined, etc.)
//   - Tất cả password = "Volley@123" (PBKDF2-SHA256 hash)
//
// GUID STRATEGY:
//   Dùng Guid.NewGuid() để tránh conflict nếu chạy lại với DB cũ có data.
//   (Nếu dùng Guid deterministic thì lần seed thứ 2 sẽ bị duplicate PK)
// ============================================================
using Microsoft.EntityFrameworkCore;
using VolleySquad.Api.Domain;
using VolleySquad.Api.Domain.Enums;
using VolleySquad.Api.Infrastructure;

namespace VolleySquad.Api.Infrastructure
{
    public static class DbSeeder
    {
        // Password cho tất cả tài khoản demo: "Volley@123"
        // Format: pbkdf2-sha256.{iterations}.{base64salt}.{base64hash}
        private const string DefaultPasswordHash =
            "pbkdf2-sha256.100000.+mNJW9QbS5A+1KLGx67R8A==.trfRqiVDxfrHaUN2xfxDz/np+n22RVjUke/6GK4n+Hw=";

        // Danh sách địa điểm sân bóng ở TP.HCM (thực tế)
        private static readonly string[] Courts =
        [
            "Sân UTE Quận 10",
            "Sân Tao Đàn Quận 1",
            "Sân Thống Nhất Quận 10",
            "Sân TDTT Quận 3",
            "Sân Him Lam Bình Chánh",
            "Sân Phan Đình Phùng Phú Nhuận",
            "Sân RMIT Quận 7",
            "Sân HUTECH Bình Thạnh",
            "Sân UEH Quận 4",
        ];

        /// <summary>
        /// Entry point được gọi từ Program.cs khi app khởi động.
        /// Chỉ seed nếu bảng Members trống (idempotent).
        /// </summary>
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            // Lấy scoped DbContext từ DI container
            // QUAN TRỌNG: DbContext là Scoped, không thể inject vào Singleton.
            // Phải tạo scope mới và Resolve thủ công.
            using var scope = serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Kiểm tra idempotent: Nếu đã có data thì bỏ qua
            if (await context.Members.AnyAsync())
            {
                return;
            }

            // ============================================================
            // STEP 1: TẠO 20 THÀNH VIÊN (1 Admin + 19 Members)
            // ============================================================
            var now = DateTime.UtcNow;

            var members = new List<Member>
            {
                // ADMIN
                new() {
                    Id           = Guid.NewGuid(),
                    Name         = "Tài Admin",
                    SkillPoint   = 85,
                    Balance      = 500_000,
                    Role         = "Admin",
                    PasswordHash = DefaultPasswordHash,
                },
                // MEMBERS (tên tiếng Việt đa dạng, skill từ 25-90)
                new() { Id = Guid.NewGuid(), Name = "Nguyễn Minh Tuấn",  SkillPoint = 78, Balance = 120_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Trần Thị Lan",       SkillPoint = 65, Balance =  80_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Lê Văn Hùng",        SkillPoint = 55, Balance = 200_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Phạm Thị Hoa",       SkillPoint = 72, Balance =  50_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Hoàng Quốc Bảo",     SkillPoint = 90, Balance = 350_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Võ Thị Thu",         SkillPoint = 48, Balance =  30_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Đặng Văn Phúc",      SkillPoint = 82, Balance = 180_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Bùi Thị Ngọc",       SkillPoint = 60, Balance =  95_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Ngô Xuân Trường",    SkillPoint = 35, Balance =  10_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Đinh Thị Mai",       SkillPoint = 70, Balance = 140_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Phan Văn Đức",       SkillPoint = 88, Balance = 420_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Lý Thị Thanh",       SkillPoint = 42, Balance =  25_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Trương Quang Vinh",  SkillPoint = 67, Balance = 110_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Mai Thị Phương",     SkillPoint = 53, Balance =  60_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Đỗ Minh Khoa",       SkillPoint = 76, Balance = 230_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Hồ Thị Bích",        SkillPoint = 38, Balance =  15_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Cao Văn Nam",         SkillPoint = 80, Balance = 280_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Lưu Thị Yến",        SkillPoint = 58, Balance =  75_000, Role = "Member", PasswordHash = DefaultPasswordHash },
                new() { Id = Guid.NewGuid(), Name = "Tạ Quốc Huy",        SkillPoint = 25, Balance =   5_000, Role = "Member", PasswordHash = DefaultPasswordHash },
            };

            context.Members.AddRange(members);

            // ============================================================
            // STEP 2: TẠO 35 TRẬN ĐẤU (30 quá khứ + 5 tương lai)
            // ============================================================
            var random = new Random(42); // seed cố định → cùng kết quả mỗi lần seed
            var allMemberIds = members.Select(m => m.Id).ToList();

            var matches = new List<Match>();

            // 30 trận trong quá khứ (6 tháng gần nhất, cách nhau ~6 ngày)
            for (var i = 0; i < 30; i++)
            {
                var daysAgo = (i + 1) * 6;
                var playDate = now.AddDays(-daysAgo);
                var court = Courts[i % Courts.Length];
                var maxSlots = random.Next(0, 3) == 0 ? 12 : 18; // 1/3 trận 12 slot

                // Chọn ngẫu nhiên 12-18 thành viên tham gia
                var participantCount = Math.Min(maxSlots, random.Next(10, 20));
                var participants = allMemberIds
                    .OrderBy(_ => random.Next())
                    .Take(participantCount)
                    .ToList();

                // Xác định trạng thái: ~60% Settled, ~40% Finished
                var status = random.Next(0, 10) < 6
                    ? MatchStatus.Settled
                    : MatchStatus.Finished;

                var match = new Match
                {
                    Id                    = Guid.NewGuid(),
                    PlayDate              = playDate,
                    Location              = court,
                    MaxSlots              = maxSlots,
                    RegisteredMemberIds   = participants,
                    Status                = status,
                    IsSettled             = status == MatchStatus.Settled,
                    FeePerPerson          = status == MatchStatus.Settled
                                            ? (random.Next(4, 9) * 10_000) // 40k-80k
                                            : 0,
                };

                matches.Add(match);
            }

            // 5 trận tương lai (2 tuần tới)
            for (var i = 0; i < 5; i++)
            {
                var daysAhead = (i + 1) * 3;
                var playDate = now.AddDays(daysAhead);
                var court = Courts[(i + 3) % Courts.Length];

                // Trận tương lai ít người đăng ký hơn
                var participantCount = random.Next(4, 15);
                var participants = allMemberIds
                    .OrderBy(_ => random.Next())
                    .Take(participantCount)
                    .ToList();

                var match = new Match
                {
                    Id                  = Guid.NewGuid(),
                    PlayDate            = playDate,
                    Location            = court,
                    MaxSlots            = 18,
                    RegisteredMemberIds = participants,
                    Status              = MatchStatus.Upcoming,
                    IsSettled           = false,
                    FeePerPerson        = 0,
                };

                matches.Add(match);
            }

            context.Matches.AddRange(matches);

            // ============================================================
            // STEP 3: TẠO ~200+ MATCH ACTIVITY RECORDS
            // ============================================================
            // Thứ tự: Tạo activities trải đều theo thời gian thực tế
            // Mỗi activity có OccurredAt riêng để feed timeline hợp lý
            var activities = new List<MatchActivity>();
            var matchCounter = 1; // Số hiệu trận (#1, #2, ...)

            // --- A. Activity: MemberRegistered (1 per member, spread over past 8 months) ---
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                var registeredAt = now.AddDays(-(180 + i * 4)); // cách đều nhau ~4 ngày
                var act = MatchActivity.MemberRegistered(member.Id, member.Name, member.SkillPoint);
                act.OccurredAt = registeredAt;
                activities.Add(act);
            }

            // --- B. Activities cho từng trận (quá khứ) ---
            foreach (var match in matches.Where(m => m.Status != MatchStatus.Upcoming))
            {
                var matchNum = matchCounter++;
                var playDate = match.PlayDate;

                // B1: MatchCreated (3 ngày trước play date)
                var createAct = MatchActivity.MatchCreated(match.Id, matchNum, playDate, match.Location);
                createAct.OccurredAt = playDate.AddDays(-3);
                activities.Add(createAct);

                // B2: MemberJoinedMatch (mỗi thành viên đăng ký, trải từ D-3 đến D-1)
                foreach (var memberId in match.RegisteredMemberIds.Take(10)) // Limit để tránh quá nhiều activities
                {
                    var member = members.First(m => m.Id == memberId);
                    var joinAct = MatchActivity.MemberJoinedMatch(memberId, match.Id, member.Name, matchNum);
                    // Rải đều trong 48h trước trận
                    var hoursBeforeMatch = random.Next(24, 72);
                    joinAct.OccurredAt = playDate.AddHours(-hoursBeforeMatch);
                    activities.Add(joinAct);
                }

                // B3: MatchFullyBooked nếu đủ slot
                if (match.RegisteredMemberIds.Count >= match.MaxSlots)
                {
                    var fullAct = MatchActivity.MatchFullyBooked(match.Id, matchNum, match.MaxSlots);
                    fullAct.OccurredAt = playDate.AddHours(-12);
                    activities.Add(fullAct);
                }

                // B4: MatchFinished (vào ngày chơi)
                var scores = GenerateScore(random);
                var finishAct = MatchActivity.MatchFinished(match.Id, matchNum, scores.Item1, scores.Item2);
                finishAct.OccurredAt = playDate.AddHours(2); // Trận xong sau 2 tiếng
                activities.Add(finishAct);

                // B5: RankingUpdated (sau khi finish ~30 phút, Ranking.Worker xử lý)
                var rankAct = MatchActivity.RankingUpdated(match.Id, matchNum);
                rankAct.OccurredAt = playDate.AddHours(2).AddMinutes(30);
                activities.Add(rankAct);

                // B6: SkillPointUpdated cho 2-3 thành viên nổi bật sau mỗi trận
                var updatedCount = random.Next(2, 4);
                var featuredMembers = match.RegisteredMemberIds
                    .OrderBy(_ => random.Next())
                    .Take(updatedCount)
                    .ToList();

                foreach (var memberId in featuredMembers)
                {
                    var member = members.First(m => m.Id == memberId);
                    var oldSp = member.SkillPoint;
                    var delta = random.Next(-3, 6); // -3 đến +5
                    var newSp = Math.Clamp(oldSp + delta, 1, 100);
                    var spAct = MatchActivity.SkillPointUpdated(memberId, member.Name, oldSp, newSp);
                    spAct.OccurredAt = rankAct.OccurredAt.AddMinutes(random.Next(1, 10));
                    activities.Add(spAct);
                }

                // B7: MatchSettled nếu đã chốt tiền
                if (match.Status == MatchStatus.Settled)
                {
                    var settleAct = MatchActivity.MatchSettled(
                        match.Id, matchNum, match.FeePerPerson, match.RegisteredMemberIds.Count);
                    settleAct.OccurredAt = playDate.AddHours(3);
                    activities.Add(settleAct);
                }
            }

            // --- C. Activities cho trận tương lai ---
            foreach (var match in matches.Where(m => m.Status == MatchStatus.Upcoming))
            {
                var matchNum = matchCounter++;
                var playDate = match.PlayDate;

                // C1: MatchCreated (hôm qua hoặc vài ngày trước)
                var createAct = MatchActivity.MatchCreated(match.Id, matchNum, playDate, match.Location);
                createAct.OccurredAt = now.AddDays(-random.Next(1, 4));
                activities.Add(createAct);

                // C2: Một số thành viên đã đăng ký
                foreach (var memberId in match.RegisteredMemberIds.Take(5))
                {
                    var member = members.First(m => m.Id == memberId);
                    var joinAct = MatchActivity.MemberJoinedMatch(memberId, match.Id, member.Name, matchNum);
                    joinAct.OccurredAt = now.AddHours(-random.Next(1, 24));
                    activities.Add(joinAct);
                }
            }

            // --- D. Milestone activities cho thành viên đạt mốc skill ---
            // Bronze (50sp), Silver (70sp), Gold (90sp)
            foreach (var member in members)
            {
                if (member.SkillPoint >= 90)
                {
                    var act = MatchActivity.MilestoneReached(member.Id, member.Name, 90);
                    act.OccurredAt = now.AddDays(-random.Next(10, 60));
                    activities.Add(act);
                }
                else if (member.SkillPoint >= 70)
                {
                    var act = MatchActivity.MilestoneReached(member.Id, member.Name, 70);
                    act.OccurredAt = now.AddDays(-random.Next(20, 90));
                    activities.Add(act);
                }
                else if (member.SkillPoint >= 50)
                {
                    var act = MatchActivity.MilestoneReached(member.Id, member.Name, 50);
                    act.OccurredAt = now.AddDays(-random.Next(30, 120));
                    activities.Add(act);
                }
            }

            context.MatchActivities.AddRange(activities);

            // ============================================================
            // STEP 4: LƯU TẤT CẢ VÀO DB TRONG 1 TRANSACTION
            // ============================================================
            // SaveChangesAsync() mặc định wrap trong 1 transaction.
            // Nếu có lỗi (ví dụ duplicate key), toàn bộ sẽ rollback.
            await context.SaveChangesAsync();
        }

        // ============================================================
        // HELPER: Tạo tỷ số bóng chuyền ngẫu nhiên hợp lệ
        // ============================================================
        // Bóng chuyền: Best of 5 sets. Thắng khi 3 sets.
        // Đây là mô phỏng đơn giản, không cần chính xác hoàn toàn.
        private static (string, string) GenerateScore(Random random)
        {
            // Kết quả phổ biến: 3-0, 3-1, 3-2
            var outcomes = new[] { ("3", "0"), ("3", "1"), ("3", "2"), ("2", "3"), ("1", "3"), ("0", "3") };
            var pick = outcomes[random.Next(outcomes.Length)];
            return (pick.Item1, pick.Item2);
        }
    }
}
