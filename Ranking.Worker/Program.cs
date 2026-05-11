// ============================================================
// RANKING WORKER - Background Service
// ============================================================
// Worker Service là một .NET Generic Host chạy nền (không có HTTP endpoint).
// Phù hợp cho: xử lý queue, scheduled jobs, file monitoring.
// Trong Docker/k8s: chạy như 1 pod riêng độc lập với API.
//
// Host.CreateApplicationBuilder vs WebApplication.CreateBuilder:
//   Host.CreateApplicationBuilder:   Generic Host — không có HTTP pipeline
//   WebApplication.CreateBuilder:    Web Host — kế thừa Generic Host + thêm HTTP
// Worker dùng Generic Host vì không cần phục vụ HTTP request.
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Ranking.Worker;
using VolleySquad.Api.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Đăng ký DbContext để truy cập DB cập nhật SkillPoint
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ⚠️ SECURITY FIX: Đọc RabbitMQ credentials từ config thay vì hardcode.
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<MatchFinishedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        // ReceiveEndpoint: Tạo queue "ranking-queue" trên RabbitMQ nếu chưa có.
        // Queue là durable (tồn tại kể cả khi RabbitMQ restart) theo mặc định của MassTransit.
        cfg.ReceiveEndpoint("ranking-queue", e =>
        {
            e.ConfigureConsumer<MatchFinishedConsumer>(context);
        });
    });
});

var host = builder.Build();
host.Run();
