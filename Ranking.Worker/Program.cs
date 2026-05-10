using MassTransit;
using Ranking.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<MatchFinishedConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", "/", h => {
            h.Username("guest");
            h.Password("guest");
        });

        cfg.ReceiveEndpoint("ranking-queue", e =>
        {
            e.ConfigureConsumer<MatchFinishedConsumer>(context);
        });
    });
});

var host = builder.Build();
host.Run();
