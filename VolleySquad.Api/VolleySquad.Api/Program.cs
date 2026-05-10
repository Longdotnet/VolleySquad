// ============================================================
// PROGRAM.CS - Điểm khởi đầu của toàn bộ ứng dụng ASP.NET Core
// ============================================================
// ASP.NET Core dùng mô hình "Generic Host" (WebApplication.CreateBuilder).
// Toàn bộ quá trình khởi động gồm 2 giai đoạn rõ ràng:
//   1) CONFIGURE SERVICES  (builder.Services.AddXxx) => Đăng ký dependency vào DI Container
//   2) CONFIGURE PIPELINE  (app.UseXxx)              => Cấu hình middleware xử lý request
// ============================================================

using VolleySquad.Api.Services;
using Microsoft.EntityFrameworkCore;
using VolleySquad.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using MassTransit;

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

// TeamService dùng AddScoped vì nó phụ thuộc vào AppDbContext (Scoped).
// Nếu đăng ký AddSingleton thì sẽ bị lỗi Captive Dependency ở trên.
builder.Services.AddScoped<TeamService>();

// CORS: Cho phép React FE (localhost:5173) gọi API trong môi trường dev
builder.Services.AddCors(options =>
{
    options.AddPolicy("DevFrontend", policy =>
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,  // Bắt buộc: kiểm tra chữ ký token có khớp SecretKey không
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,           // Project nhỏ: tắt check Issuer (URL phát hành token)
            ValidateAudience = false,          // Project nhỏ: tắt check Audience (đối tượng token)
            // FIX: ClockSkew mặc định là 5 phút (server gia hạn token thêm 5 phút sau expires).
            // Đặt về Zero để token hết hạn đúng thời điểm đã set.
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// MassTransit với RabbitMQ
builder.Services.AddMassTransit(x =>
{
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();

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

// Tự động chuyển hướng HTTP sang HTTPS (bảo mật truyền tải)
app.UseHttpsRedirection();

// Bước 1: Đọc JWT từ header "Authorization: Bearer <token>",
//         giải mã, validate, và tạo ra ClaimsPrincipal (User object)
app.UseAuthentication();

// Bước 2: Dùng ClaimsPrincipal vừa có để kiểm tra xem endpoint
//         có yêu cầu [Authorize] hay không, và role có khớp không.
app.UseAuthorization();

// Ánh xạ request đến đúng Controller và Action method tương ứng
app.MapControllers();

app.Run();
