using LiveQuizApp.Hubs;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();

// The Angular dev server runs on a different port (4200) than this
// backend (5000), so the browser treats them as different origins.
// SignalR needs CORS enabled - and AllowCredentials is required
// specifically because SignalR's WebSocket handshake sends credentials.
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDev", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("AngularDev");

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<QuizHub>("/quizHub");

app.Run();
