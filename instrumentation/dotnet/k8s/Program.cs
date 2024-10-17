using SplunkTelemetry;
using Microsoft.Extensions.Logging.Console;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

// Reference:  https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-8.0#automatically-log-scope-with-spanid-traceid-parentid-baggage-and-tags
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
});

builder.Logging.Configure(options =>
{
    options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId
                                       | ActivityTrackingOptions.TraceId
                                       | ActivityTrackingOptions.ParentId
                                       | ActivityTrackingOptions.Baggage
                                       | ActivityTrackingOptions.Tags;
}).AddConsole(options =>
{
   options.FormatterName = "splunkLogsJson";
});
builder.Logging.AddConsoleFormatter<SplunkTelemetryConsoleFormatter, ConsoleFormatterOptions>();

var app = builder.Build();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=HelloWorld}/{action=Index}");

app.Run();
