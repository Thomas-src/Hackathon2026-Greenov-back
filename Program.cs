using Backend.Catalog.Models;
using Backend.Domains;
using Backend.Hubs;
using Backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true); // dev only
    });
});

builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<EventCatalog>();
builder.Services.AddSingleton<ActionEngine>();

var app = builder.Build();

app.UseCors();

app.MapHub<QuizHub>("/hub");
app.MapFallback(() => Results.NotFound());

app.Run();