// ============================================================
// PROGRAM.CS - Điểm khởi đầu của toàn bộ ứng dụng ASP.NET Core
// ============================================================
// ASP.NET Core dùng mô hình "Generic Host" (WebApplication.CreateBuilder).
// Toàn bộ quá trình khởi động gồm 2 giai đoạn rõ ràng:
//   1) CONFIGURE SERVICES  (builder.Services.AddXxx) => Đăng ký dependency vào DI Container
//   2) CONFIGURE PIPELINE  (app.UseXxx)              => Cấu hình middleware xử lý request
// ============================================================

using VolleySquad.Api.Services;
using VolleySquad.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using VolleySquad.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using System.Text;
using MassTransit;
using VolleySquad.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// GIAI ĐOẠN 1: ĐĂNG KÝ SERVICES (Dependency Injection Container)
// ============================================================
// DI (Dependency Injection) giải quyết vấn đề:
// "Lớp A cần lớp B, ai tạo B và quản lý vòng đời của nó?"
// Thay vì A tự new B(), ta đăng ký B vào DI Container và để nó inject.
//
// 3 LOẠI VÒNG ĐỜI (Service Lifetime) - Câu hỏi phỏng vấn phổ biến:
// ┌────────────────────────────────────────────────────────────────────┐
// │ AddSingleton  │ 1 instance duy nhất cho toàn bộ app (app lifetime)│
// │               │ Dùng cho: config, logging, caching, HttpClient     │
// │ AddScoped     │ 1 instance mới cho MỖI HTTP request               │
// │               │ Dùng cho: DbContext, repository, business service  │
// │ AddTransient  │ 1 instance mới mỗi LẦN được inject                │
// │               │ Dùng cho: lightweight/stateless utility class      │
// └────────────────────────────────────────────────────────────────────┘
//
// ⚠️ CAPTIVE DEPENDENCY (lỗi phổ biến):
//    KHÔNG inject Scoped/Transient vào Singleton!
//    Singleton sống mãi => Scoped bị "giam" sống theo => stale data, memory leak.

// AddScoped (best practice cho EF Core):
// Mỗi HTTP request tạo 1 DbContext riêng, dùng xong thì Dispose tự động.
// DbContext KHÔNG thread-safe => nếu dùng Singleton sẽ bị lỗi concurrent access.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// TeamService dùng AddSingleton vì:
//   1. STATELESS: Không có field instance, chỉ xử lý input và trả output.
//   2. THREAD-SAFE: Chỉ dùng local variables trong method, không shared state.
//   3. KHÔNG inject Scoped dependency (AppDbContext) vào constructor.
//   → Tạo 1 lần khi app start, dùng cho mọi request. Tiết kiệm GC overhead.
//
// So sánh với MemberService (Scoped):
//   MemberService inject AppDbContext (Scoped) → phải là Scoped.
//   TeamService KHÔNG inject AppDbContext → có thể là Singleton.
//
// ⚠️ CAPTIVE DEPENDENCY RULE:
//   Singleton KHÔNG được inject Scoped/Transient service.
//   Singleton ≥ Scoped ≥ Transient (lifetime phải >= dependency's lifetime).
builder.Services.AddSingleton<ITeamService, TeamService>();

// MemberService dùng AddScoped vì inject AppDbContext (Scoped).
// Interface binding: IMemberService → MemberService.
// Controller inject IMemberService, không biết MemberService tồn tại.
// → Testable: Trong unit test, inject Mock<IMemberService>() thay thế.
builder.Services.AddScoped<IMemberService, MemberService>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();

// ============================================================
// RATE LIMITING - Chống Brute Force & DoS (OWASP A05, A04)
// ============================================================
// OWASP Top 10:
//   A05 Security Misconfiguration: Thiếu rate limiting = cấu hình sai
//   A04 Insecure Design: Cho phép tấn công brute force vào endpoint login
//
// Fixed Window vs Sliding Window vs Token Bucket:
//   Fixed Window:   100 req / 1 phút. Vấn đề: burst tấn công cuối cửa sổ này + đầu cửa sổ tiếp
//   Sliding Window: Đếm req trong 60 giây gần nhất — chính xác hơn, tốn RAM hơn
//   Token Bucket:   Đầy token thì mới cho req — cho phép burst ngắn tự nhiên
//
// .NET 7+ có built-in RateLimiter trong System.Threading.RateLimiting.
// Đây là Fixed Window đơn giản cho demo; production nên dùng Redis-backed rate limiter
// để hoạt động đúng trong môi trường nhiều server (horizontal scaling).
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("LoginPolicy", opt =>
    {
        opt.PermitLimit = 10;               // Tối đa 10 lần login
        opt.Window = TimeSpan.FromMinutes(1); // trong vòng 1 phút
        opt.QueueLimit = 0;                 // Không xếp hàng chờ
    });

    // Trả 429 Too Many Requests (đúng HTTP spec)
    options.RejectionStatusCode = 429;
});

// ============================================================
// CORS - Cross-Origin Resource Sharing
// ============================================================
// Same-Origin Policy: Browser mặc định chặn request từ origin A đến origin B.
//   Frontend: http://localhost:5173 (origin A)
//   Backend:  https://localhost:7202 (origin B — khác port = khác origin)
//
// CORS header server trả về: Access-Control-Allow-Origin: http://localhost:5173
// Browser đọc header này và mới cho phép JS đọc response.
//
// AllowAnyHeader + AllowAnyMethod: OK cho development.
// PRODUCTION: Hạn chế Method (GET, POST, PUT, DELETE) và Header cụ thể.
// AllowCredentials: Bắt buộc cho SignalR WebSocket (gửi cookie/header auth qua WS).
// Đọc danh sách allowed origins từ config (hỗ trợ thêm IP điện thoại mà không cần sửa code)
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:5173", "https://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevFrontend", policy =>
        // SetIsOriginAllowed cho phép CORS động (AllowAnyOrigin() không tương thích với AllowCredentials())
        policy.SetIsOriginAllowed(origin =>
                  allowedOrigins.Any(allowed => origin.Equals(allowed, StringComparison.OrdinalIgnoreCase))
              )
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials());
});

// SignalR: Real-time communication hub
builder.Services.AddSignalR();

builder.Services.AddControllers();

// Swagger/OpenAPI: Tự động sinh tài liệu API từ code (annotation, route, model)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "VolleySquad API", Version = "v1" });

    // Cấu hình SecurityDefinition để hiển thị nút "Authorize" trên Swagger UI.
    // Type = Http + Scheme = Bearer => field chỉ cần nhập token, không cần gõ "Bearer " prefix.
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập JWT token. Ví dụ: eyJhbGci..."
    });

    // SecurityRequirement: Áp dụng Bearer auth cho tất cả endpoint trong Swagger UI.
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ============================================================
// CẤU HÌNH AUTHENTICATION & AUTHORIZATION
// ============================================================
// Authentication = "Bạn là ai?"        => Xác thực danh tính (JWT, Cookie, Google...)
// Authorization  = "Bạn có quyền gì?" => Kiểm tra quyền ([Authorize], [Authorize(Roles="Admin")])
//
// JWT (JSON Web Token) gồm 3 phần cách nhau bởi dấu ".":
//   eyJhbGciOiJIUzI1NiJ9  .  eyJuYW1lIjoiYWRtaW4ifQ  .  <signature>
//     Header (thuật toán)      Payload (claims/data)      Chữ ký xác thực
//
// Server KHÔNG lưu token => Stateless. Mỗi request tự mang token,
// server chỉ cần verify chữ ký bằng SecretKey.

// FIX: Đọc SecretKey từ config thay vì hardcode trong code.
// Hardcode trong code sẽ bị lộ nếu push lên Git repository!
var jwtSecret = builder.Configuration["JwtSettings:SecretKey"]
    ?? throw new InvalidOperationException("JwtSettings:SecretKey chưa được cấu hình trong appsettings.json");
var jwtIssuer = builder.Configuration["JwtSettings:Issuer"]
    ?? throw new InvalidOperationException("JwtSettings:Issuer chưa được cấu hình trong appsettings.json");
var jwtAudience = builder.Configuration["JwtSettings:Audience"]
    ?? throw new InvalidOperationException("JwtSettings:Audience chưa được cấu hình trong appsettings.json");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // FIX: SignalR WebSocket không thể gửi Authorization header từ browser.
        // Token được gắn vào query string ?access_token=<token> bởi SignalR client.
        // Middleware JWT mặc định chỉ đọc header => phải đọc thêm từ query string.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) &&
                    path.StartsWithSegments("/hubs"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ============================================================
// MASSTRANSIT + RABBITMQ - Message Broker cho Event-Driven Architecture
// ============================================================
// MassTransit là abstraction layer ở trên Message Broker.
// Tương tự như Entity Framework là abstraction layer ở trên Database.
//
// Tại sao dùng Message Broker thay vì gọi trực tiếp (synchronous)?
//   ĐỒNG BỘ (HTTP call trực tiếp):
//     API → gọi RankingService.Calculate() → CHỜ xong → trả response
//     Vấn đề: Nếu RankingService chết → API cũng thất bại
//             Nếu Calculate() mất 3 giây → client chờ 3 giây
//
//   BẤT ĐỒNG BỘ (Message Broker):
//     API → publish event vào Queue → trả response NGAY (fast!)
//     RabbitMQ giữ event trong queue
//     Ranking.Worker consume event và xử lý độc lập
//     Nếu Worker chết → event vẫn trong queue → xử lý khi Worker online lại
//
// Dead Letter Queue (DLQ):
//   Nếu Consumer crash sau N lần retry → message chuyển vào DLQ
//   Admin xem lại DLQ để debug và republish.
//
// ⚠️ SECURITY FIX: Đọc RabbitMQ credentials từ config thay vì hardcode.
// Hardcode "guest/guest" trong code:
//   1. Lộ credentials khi push lên GitHub
//   2. Production RabbitMQ không cho phép "guest" login từ remote host
//   3. Không thể thay đổi credentials mà không cần sửa code và redeploy

// Health Checks — kiểm tra trạng thái app, dùng bởi GET /health
builder.Services.AddHealthChecks();

var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";
// ⚠️ PRODUCTION: Thay giá trị mặc định bằng cách set trong môi trường:
//   dotnet user-secrets set "RabbitMQ:Password" "your-strong-password"
//   Hoặc dùng Environment Variables: RabbitMQ__Password=xxx (dấu __ = dấu : trong .NET config)

builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        // Exponential backoff khi RabbitMQ chưa chạy:
        // Thử lại 5 lần, khoảng cách tăng dần 5s → 30s → 120s
        // Tránh spam log mỗi giây vào console
        cfg.UseMessageRetry(r =>
            r.Exponential(
                retryLimit:    5,
                minInterval:   TimeSpan.FromSeconds(5),
                maxInterval:   TimeSpan.FromSeconds(120),
                intervalDelta: TimeSpan.FromSeconds(10)));

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

// ============================================================
// SEED DỮ LIỆU KHI KHỞI ĐỘNG (chỉ chạy nếu DB trống)
// ============================================================
// Gọi DbSeeder.SeedAsync() để tạo dữ liệu mẫu khi app lần đầu khởi động.
// Idempotent: SeedAsync tự kiểm tra Members.Any() trước khi insert.
// Phải tạo scope mới vì DbContext là Scoped service (không phải Singleton).
using (var seedScope = app.Services.CreateScope())
{
    await VolleySquad.Api.Infrastructure.DbSeeder.SeedAsync(seedScope.ServiceProvider);
}

// ============================================================
// GIAI ĐOẠN 2: MIDDLEWARE PIPELINE - Câu hỏi phỏng vấn quan trọng!
// ============================================================
// Middleware là các "trạm xử lý" mà MỖII HTTP request phải đi qua tuần tự.
// Hãy tưởng tượng như dây chuyền lắp ráp:
//
//  Request ─→ [Swagger] ─→ [HTTPS] ─→ [Authn] ─→ [Authz] ─→ [Controller]
//  Response ←──────────────────────────────────────────────────────────────
//
// Mỗi middleware có thể:
//   - Xử lý request rồi chuyển tiếp (next())
//   - Trả về response ngay mà không chuyển tiếp (short-circuit)
//
// ⚠️ THỨ TỰ QUYẾT ĐỊNH TẤT CẢ:
//   UseAuthentication() PHẢI trước UseAuthorization().
//   Lý do: Authorization cần biết "User là ai" mà Authentication mới tạo ra.
//   Nếu đảo ngược: [Authorize] kiểm tra nhưng User.Identity chưa được set => 401 sai.

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();    // Phục vụ file JSON schema của API
    app.UseSwaggerUI(); // Phục vụ giao diện web Swagger
}

// CORS phải đặt TRƯỚC Authentication/Authorization
app.UseCors("DevFrontend");

// Rate Limiting middleware — chặn request vượt giới hạn trước khi tới controller
app.UseRateLimiter();

// Tự động chuyển hướng HTTP sang HTTPS (bảo mật truyền tải)
// Chỉ bật trong Production — Development dùng HTTP từ điện thoại sẽ bị redirect loop
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

// Bước 1: Đọc JWT từ header "Authorization: Bearer <token>",
//         giải mã, validate, và tạo ra ClaimsPrincipal (User object)
app.UseAuthentication();

// Bước 2: Dùng ClaimsPrincipal vừa có để kiểm tra xem endpoint
//         có yêu cầu [Authorize] hay không, và role có khớp không.
app.UseAuthorization();

// Ánh xạ request đến đúng Controller và Action method tương ứng
app.MapControllers();

// SignalR Hub endpoint — client kết nối tới ws://localhost:PORT/hubs/match
app.MapHub<MatchHub>("/hubs/match");

// Health check endpoint — dùng bởi ServerStatusBar trên trang Login
// GET /health → { status: "Healthy" }
// Loại bỏ MassTransit check (RabbitMQ có thể không chạy) — chỉ check DB/app
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    // Bỏ qua health check của MassTransit (tag "masstransit") khi RabbitMQ không chạy
    // Endpoint này chỉ phản ánh trạng thái DB và API, không phụ thuộc RabbitMQ
    Predicate = check => !check.Tags.Contains("masstransit")
});

app.Run();
