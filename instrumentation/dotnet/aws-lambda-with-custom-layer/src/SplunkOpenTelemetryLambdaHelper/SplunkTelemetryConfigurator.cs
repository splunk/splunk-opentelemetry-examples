using System.IO;
using System.Text.Json;
using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging.Abstractions;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Exporter;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Resources.AWS;
using OpenTelemetry.Resources;

namespace SplunkTelemetry
{
   public static class SplunkTelemetryConfigurator
   {
       public static TracerProvider ConfigureSplunkTelemetry()
       {
         var serviceName = Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME") ?? "Unknown";
         var accessToken = Environment.GetEnvironmentVariable("SPLUNK_ACCESS_TOKEN")?.Trim();
         var realm = Environment.GetEnvironmentVariable("SPLUNK_REALM")?.Trim();

         ArgumentNullException.ThrowIfNull(accessToken, "SPLUNK_ACCESS_TOKEN");
         ArgumentNullException.ThrowIfNull(realm, "SPLUNK_REALM");

         var builder = Sdk.CreateTracerProviderBuilder()
               .AddHttpClientInstrumentation()
               .AddAWSInstrumentation()
               .SetSampler(new AlwaysOnSampler())
               .AddAWSLambdaConfigurations(opts => opts.DisableAwsXRayContextExtraction = true)
               .ConfigureResource(configure => configure
                     .AddService(serviceName, serviceVersion: "1.0.0")
                     .AddAWSEBSDetector())
               .AddOtlpExporter();

          return builder.Build()!;
       }

       public static ILogger<T> ConfigureLogger<T>()
       {
           var loggerFactory = LoggerFactory.Create(logging =>
           {
               logging.ClearProviders(); // Clear existing providers
               logging.Configure(options =>
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
               logging.AddConsoleFormatter<SplunkTelemetryConsoleFormatter, ConsoleFormatterOptions>();
           });

           return loggerFactory.CreateLogger<T>();
       }

       public static void AddSpanAttributes(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context, Activity activity)
       {
           // Add span attributes using the APIGatewayProxyRequest and ILambdaContext
           activity?.SetTag("sometag", "somevalue");
        }
   }

   public class SplunkTelemetryConsoleFormatter : ConsoleFormatter
   {
       public SplunkTelemetryConsoleFormatter() : base("splunkLogsJson") { }

       public override void Write<TState>(in LogEntry<TState> logEntry, IExternalScopeProvider? scopeProvider, TextWriter textWriter)
       {
           var serviceName = Environment.GetEnvironmentVariable("OTEL_SERVICE_NAME") ?? "Unknown";
           var severity = logEntry.LogLevel switch
           {
               Microsoft.Extensions.Logging.LogLevel.Trace => "DEBUG",
               Microsoft.Extensions.Logging.LogLevel.Debug => "DEBUG",
               Microsoft.Extensions.Logging.LogLevel.Information => "INFO",
               Microsoft.Extensions.Logging.LogLevel.Warning => "WARN",
               Microsoft.Extensions.Logging.LogLevel.Error => "ERROR",
               Microsoft.Extensions.Logging.LogLevel.Critical => "FATAL",
               Microsoft.Extensions.Logging.LogLevel.None => "NONE",
               _ => "INFO"
           };
           var logObject = new Dictionary<string, object>
           {
               { "event_id", logEntry.EventId.Id },
               { "log_level", logEntry.LogLevel.ToString().ToLower() },
               { "category", logEntry.Category },
               { "message", logEntry.Formatter(logEntry.State, logEntry.Exception) },
               { "timestamp", DateTime.UtcNow.ToString("o") },
               { "service.name", serviceName },
               { "severity", severity }
           };
           // Add exception if present
           if (logEntry.Exception != null)
           {
               logObject["exception"] = logEntry.Exception.ToString();
           }
           // Include scopes if enabled
           if (scopeProvider != null)
           {
               scopeProvider.ForEachScope((scope, state) =>
               {
                   if (scope is IReadOnlyList<KeyValuePair<string, object>> scopeItems)
                   {
                       foreach (var kvp in scopeItems)
                       {
                           if (kvp.Key.Equals("SpanId", StringComparison.OrdinalIgnoreCase))
                               logObject["span_id"] = kvp.Value;
                           else if (kvp.Key.Equals("TraceId", StringComparison.OrdinalIgnoreCase))
                               logObject["trace_id"] = kvp.Value;
                           else if (kvp.Key.Equals("ParentId", StringComparison.OrdinalIgnoreCase))
                               logObject["parent_id"] = kvp.Value;
                           else
                               logObject[kvp.Key] = kvp.Value;
                       }
                   }
                   else if (scope is IEnumerable<KeyValuePair<string, string>> baggage)
                   {
                       foreach (var kvp in baggage)
                       {
                           logObject[$"baggage_{kvp.Key}"] = kvp.Value;
                       }
                   }
                   else if (scope is IEnumerable<KeyValuePair<string, object>> tags)
                   {
                       foreach (var kvp in tags)
                       {
                           logObject[$"tag_{kvp.Key}"] = kvp.Value;
                       }
                   }
               }, logObject);
           }

           var logJson = JsonSerializer.Serialize(logObject);
           textWriter.WriteLine(logJson);
       }
   }
}