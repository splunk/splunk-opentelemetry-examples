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

### Add the Splunk OpenTelemetry Collector layer

Our example deploys the Splunk distribution of the OpenTelemetry collector
to a separate layer within the lambda function.  Lookup the ARN for your 
region in Step 6 in [this document](https://docs.splunk.com/observability/en/gdi/get-data-in/serverless/aws/otel-lambda-layer/instrumentation/lambda-language-layers.html#install-the-aws-lambda-layer-for-your-language). 

Then, open the template.yaml file and add the ARN there.  For example, 
here's the ARN for us-west-1: 

````
      Layers:
        - arn:aws:lambda:us-west-1:254067382080:layer:splunk-apm-collector:10
````

### Add the Splunk Observability Cloud Access Token and Realm

We'll also need to specify the realm and access token for the target
Splunk Observability Cloud environment.  This goes in the template.yaml
file as well: 

````
  Environment: 
    Variables:
      SPLUNK_ACCESS_TOKEN: ADD_ACCESS_TOKEN_HERE
      SPLUNK_REALM: ADD_REALM_HERE
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
