using Server.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<UdpGameServer>();

var app = builder.Build();

app.Run();