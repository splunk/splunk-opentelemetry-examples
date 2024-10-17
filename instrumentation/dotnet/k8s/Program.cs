var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.Logger.LogInformation("The app started successfully");

app.MapGet("/", () => "Hello World!");

app.Run();
