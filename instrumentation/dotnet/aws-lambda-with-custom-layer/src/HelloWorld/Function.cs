using OpenTelemetry;
using OpenTelemetry.Trace;
using OpenTelemetry.Instrumentation.AWSLambda;

using System.Diagnostics;

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

using SplunkTelemetry;

using Microsoft.Extensions.Logging;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace HelloWorld;

public class Function
{
    private static readonly TracerProvider TracerProvider;
    private static readonly ILogger<Function> _logger;
    private static readonly HttpClient client = new HttpClient();

    static Function()
    {
        TracerProvider = SplunkTelemetryConfigurator.ConfigureSplunkTelemetry()!;
        _logger = SplunkTelemetryConfigurator.ConfigureLogger<Function>();
    }

    // Note: Do not forget to point function handler to here.
    public Task<APIGatewayProxyResponse> TracingFunctionHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
      => AWSLambdaWrapper.Trace(TracerProvider, FunctionHandler, apigProxyEvent, context);

    private static async Task<string> GetCallingIP()
    {
        _logger.LogInformation("Getting the Calling IP");

        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Add("User-Agent", "AWS Lambda .Net Client");

        var msg = await client.GetStringAsync("http://checkip.amazonaws.com/").ConfigureAwait(continueOnCapturedContext:false);
        var location = msg.Replace("\n","");


        return location;
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(APIGatewayProxyRequest apigProxyEvent, ILambdaContext context)
    {
        _logger.LogInformation("In the function handler");

        // add span attributes based on the provide API gateway event and Lambda context
        SplunkTelemetryConfigurator.AddSpanAttributes(apigProxyEvent, context);

        var location = await GetCallingIP();

        var body = new Dictionary<string, string>
        {
            { "message", "hello world" },
            { "location", location }
        };

        _logger.LogInformation("Returning response: {@Body}", body);

        return new APIGatewayProxyResponse
        {
            Body = JsonSerializer.Serialize(body),
            StatusCode = 200,
            Headers = new Dictionary<string, string> { { "Content-Type", "application/json" } }
        };
    }
}