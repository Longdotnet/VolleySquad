using MassTransit;
using Microsoft.EntityFrameworkCore;
using Payment.Worker;
using VolleySquad.Api.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

// Đăng ký DbContext để trừ Balance thành viên
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ⚠️ SECURITY FIX: Đọc RabbitMQ credentials từ config thay vì hardcode.
var rabbitHost = builder.Configuration["RabbitMQ:Host"] ?? "localhost";
var rabbitUser = builder.Configuration["RabbitMQ:Username"] ?? "guest";
var rabbitPass = builder.Configuration["RabbitMQ:Password"] ?? "guest";

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<MatchFinalizedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(rabbitHost, "/", h =>
        {
            h.Username(rabbitUser);
            h.Password(rabbitPass);
        });

        cfg.ReceiveEndpoint("payment-queue", e =>
        {
            e.ConfigureConsumer<MatchFinalizedConsumer>(context);
        });
    });
});

var host = builder.Build();
host.Run();
