# Instrumenting a .NET AWS Lambda Function with OpenTelemetry

This example demonstrates how to instrument an AWS Lambda function written in
.NET using OpenTelemetry, and then export the data to Splunk Observability 
Cloud.  We'll use .NET 8 for this example, but the steps for .NET 6 are 
similar.  The example also uses the AWS Serverless Application Model (SAM)
CLI to deploy the Lambda function and an associated API Gateway to access it. 

## Prerequisites 

The following tools are required to deploy .NET functions into AWS Lambda: 

* An AWS account with permissions to create and execute Lambda functions
* Download and install the [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
* Download and install [AWS SAM](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/install-sam-cli.html)

## Application Overview

If you just want to build and deploy the example, feel free to skip this section. 

The application used here is based on the "Hello World" example application that's part of the 
[AWS Quick Start templates](https://docs.aws.amazon.com/serverless-application-model/latest/developerguide/using-sam-cli-init.html). 

We added a helper class named [SplunkTelemetryConfiguration](./src/HelloWorld/SplunkTelemetryConfigurator.cs), and included code to 
assist with initializing the tracer, as well as a custom logger to inject the trace context. 

The tracer initialization is based on the example found in 
[Instrument your .NET AWS Lambda function for Splunk Observability Cloud](https://docs.splunk.com/observability/en/gdi/get-data-in/serverless/aws/otel-lambda-layer/instrumentation/dotnet-lambdas.html): 

````
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
````

The custom logger injects the trace context as follows: 

````
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
````

The [Lambda function](./src/HelloWorld/Function.cs) was then modified to configure 
OpenTelemetry using the helper class as follows: 

````
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
    ...
}
````

These code changes required a number of packages to be added to the HelloWorld.csproj file: 

````
  <ItemGroup>
    ...
    <PackageReference Include="OpenTelemetry" Version="1.9.0" />
    <PackageReference Include="OpenTelemetry.Exporter.OpenTelemetryProtocol" Version="1.9.0" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AWS" Version="1.1.0-beta.4" />
    <PackageReference Include="OpenTelemetry.Instrumentation.AWSLambda" Version="1.3.0-beta.1" />
    <PackageReference Include="OpenTelemetry.Instrumentation.Http" Version="1.9.0" />
    <PackageReference Include="OpenTelemetry.Resources.AWS" Version="1.5.0-beta.1" />
    <PackageReference Include="Microsoft.Extensions.Logging" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="8.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="8.0.0" />
  </ItemGroup>
````

The provided template.yaml.base file was then updated to use `TracingFunctionHandler` as the handler
rather than the default `FunctionHandler`: 

````
Resources:
  HelloWorldFunction:
    Type: AWS::Serverless::Function
    Properties:
      CodeUri: ./src/HelloWorld/
      Handler: HelloWorld::HelloWorld.Function::TracingFunctionHandler
````

We also added the SPLUNK_ACCESS_TOKEN and SPLUNK_REALM environment variables to the 
template.yaml.base file, as well as a layer for the Splunk Observability Collector. 

## Build and Deploy

Open a command line terminal and navigate to the root of the directory.  
For example: 

````
cd ~/splunk-opentelemetry-examples/instrumentation/dotnet/aws-lambda
````

### Provide your AWS credentials

````
export AWS_ACCESS_KEY_ID="<put the access key ID here>"
export AWS_SECRET_ACCESS_KEY="<put the secret access key here>"
export AWS_SESSION_TOKEN="<put the session token here>"
````

### Add the Splunk OpenTelemetry Collector and Metrics Extension layers

Our example deploys the Splunk distribution of the OpenTelemetry collector
to a separate layer within the lambda function.  Lookup the ARN for your 
region in Step 6 in [this document](https://docs.splunk.com/observability/en/gdi/get-data-in/serverless/aws/otel-lambda-layer/instrumentation/lambda-language-layers.html#install-the-aws-lambda-layer-for-your-language). 

Let's make a copy of the template.yaml.base file: 

````
cp template.yaml.base template.yaml
````

Then, open the template.yaml file and add the ARN there.  For example, 
here's the ARN for us-west-1: 

````
      Layers:
        - arn:aws:lambda:us-west-1:254067382080:layer:splunk-apm-collector:10
````

Optionally, we can also add the Splunk Metrics Extension Layer to the template.yaml file.
Lookup the ARN for your
region in Step 7 in [this document](https://docs.splunk.com/observability/en/gdi/get-data-in/serverless/aws/otel-lambda-layer/instrumentation/lambda-language-layers.html#install-the-aws-lambda-layer-for-your-language).

````
      Layers:
        - arn:aws:lambda:us-west-1:254067382080:layer:splunk-apm-collector:10
        - arn:aws:lambda:us-west-1:254067382080:layer:splunk-lambda-metrics:10
````

### Add the Splunk Observability Cloud Access Token and Realm

We'll also need to specify the realm and access token for the target
Splunk Observability Cloud environment.  This goes in the template.yaml
file as well: 

````
  Environment: 
    Variables:
      SPLUNK_ACCESS_TOKEN: <access token>
      SPLUNK_REALM: us1
      OTEL_RESOURCE_ATTRIBUTES: deployment.environment=test
````

### Build the SAM Function

Next, we'll build the function using SAM: 

````
sam build
````
### Deploy the SAM Function

Then deploy it: 

````
sam deploy --guided
````

You'll be asked a number of questions along the way.  Here are sample responses, 
but you should provide the desired stack name and AWS region for your lambda 
function. 

````
Setting default arguments for 'sam deploy'
=========================================
Stack Name [sam-app]: dotnet8-lambda-test
AWS Region [eu-west-1]: us-west-1
#Shows you resources changes to be deployed and require a 'Y' to initiate deploy
Confirm changes before deploy [y/N]: y
#SAM needs permission to be able to create roles to connect to the resources in your template
Allow SAM CLI IAM role creation [Y/n]: y
#Preserves the state of previously provisioned resources when an operation fails
Disable rollback [y/N]: n
HelloWorldFunction has no authentication. Is this okay? [y/N]: y
Save arguments to configuration file [Y/n]: y
SAM configuration file [samconfig.toml]: 
SAM configuration environment [default]: 
````

It will take a few moments for SAM to create all of the objects necessary to 
support your lambda function.  Once it's ready, it will provide you with an API 
Gateway Endpoint URL that uses the following format: 

````
https://${ServerlessRestApi}.execute-api.${AWS::Region}.amazonaws.com/Prod/hello/
````

### Test the SAM Function

Use the API Gateway Endpoint URL provided in the previous step to test the SAM function. 
You should see a response such as the following: 

````
{"message":"hello world","location":"54.177.68.123"}
````

### View Traces in Splunk Observability Cloud

After a minute or so, you should start to see traces for the lambda function
appearing in Splunk Observability Cloud: 

![Trace](./images/trace.png)

### View Metrics in Splunk Observability Cloud

If you added the Splunk Metrics Extension Layer, you'll also see metrics for your
lambda function by navigating to Infrastructure -> Lambda functions (OTel) and 
then selecting your lambda function: 

![Trace](./images/lambda-dashboard.png)

### View Logs in CloudWatch

Lambda functions send their logs to AWS CloudWatch.  In the following example, 
we can see that the trace context was injected successfully into the logs 
using the custom logging changes described above: 

````
{
    "event_id": 0,
    "log_level": "information",
    "category": "HelloWorld.Function",
    "message": "Returning response: [message, hello world], [location, 3.101.146.139]",
    "timestamp": "2024-10-24T21:28:04.1522807Z",
    "service.name": "Unknown",
    "severity": "INFO",
    "span_id": "9a6e3418afc4e5f7",
    "trace_id": "326333ab1b4b015f1affa32d48fa12b6",
    "parent_id": "0000000000000000",
    "tag_faas.trigger": "http",
    "tag_faas.name": "dotnet8-test-HelloWorldFunction-BEIwuCTr5dLQ",
    "tag_faas.execution": "af34b643-776c-456a-ae49-bbd70bae37d5",
    "tag_faas.id": "arn:aws:lambda:us-west-1:539****08140:function:dotnet8-test-HelloWorldFunction-BEIwuCTr5dLQ",
    "tag_cloud.account.id": "539****08140",
    "tag_http.scheme": "https",
    "tag_http.target": "/Prod/hello/",
    "tag_http.method": "GET",
    "tag_net.host.name": "5uai5jfjwk.execute-api.us-west-1.amazonaws.com",
    "tag_net.host.port": 443
}
````
