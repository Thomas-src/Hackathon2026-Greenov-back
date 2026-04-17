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

builder.Services.AddSingleton<RoomService>();

var app = builder.Build();

app.UseCors();

app.MapHub<QuizHub>("/quizhub");

app.Run();