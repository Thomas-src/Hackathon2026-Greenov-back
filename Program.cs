using System.Text.Json;
using Backend.Catalog.Models;
using Backend.Domains;
using Backend.Hubs;
using Backend.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()
        );
        options.PayloadSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
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
builder.Services.AddControllers();

builder.Services.AddSingleton<RoomManager>();
builder.Services.AddSingleton<EventCatalog>();
builder.Services.AddSingleton<ActionEngine>();

var app = builder.Build();

app.UseCors();

app.MapHub<QuizHub>("/hub");
app.MapControllers();

app.Run();