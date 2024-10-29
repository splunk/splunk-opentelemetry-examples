package main

import (
    "context"
	"encoding/json"
	"fmt"
	"net/http"

	"github.com/aws/aws-lambda-go/events"
	"github.com/aws/aws-lambda-go/lambda"
    "go.opentelemetry.io/otel"
    "github.com/signalfx/splunk-otel-go/distro"
    "go.opentelemetry.io/contrib/instrumentation/github.com/aws/aws-lambda-go/otellambda"
    "go.opentelemetry.io/otel/propagation"
    "go.opentelemetry.io/otel/trace"

    "go.uber.org/zap"
)

var logger *zap.Logger

func handler(ctx context.Context, request events.APIGatewayProxyRequest) (events.APIGatewayProxyResponse, error) {

    loggerWithTraceContext := withTraceMetadata(ctx, logger)

	var greeting string

	loggerWithTraceContext.Info("About to retrieve the SourceIP")

	sourceIP := request.RequestContext.Identity.SourceIP

	loggerWithTraceContext.Info("Successfully retrieved the SourceIP, returning the greeting")

	if sourceIP == "" {
		greeting = "Hello, world!\n"
	} else {
		greeting = fmt.Sprintf("Hello, %s!\n", sourceIP)
	}

	return events.APIGatewayProxyResponse{
		Body:       greeting,
		StatusCode: 200,
	}, nil
}

var traceparent = http.CanonicalHeaderKey("traceparent")

func customEventToCarrier(eventJSON []byte) propagation.TextMapCarrier {
	var request events.APIGatewayProxyRequest
	_ = json.Unmarshal(eventJSON, &request)

	var header = http.Header{
		traceparent: []string{request.Headers["traceparent"]},
	}

	return propagation.HeaderCarrier(header)
}

func withTraceMetadata(ctx context.Context, logger *zap.Logger) *zap.Logger {
        spanContext := trace.SpanContextFromContext(ctx)
        if !spanContext.IsValid() {
                // ctx does not contain a valid span.
                // There is no trace metadata to add.
                return logger
        }
        return logger.With(
                zap.String("trace_id", spanContext.TraceID().String()),
                zap.String("span_id", spanContext.SpanID().String()),
                zap.String("trace_flags", spanContext.TraceFlags().String()),
        )
}

func main() {
	ctx := context.Background()

	sdk, err := distro.Run()
	if err != nil {
		panic(err)
	}
	// Flush all spans before the application exits
	defer func() {
		if err := sdk.Shutdown(ctx); err != nil {
			panic(err)
		}
	}()

    logger, err = zap.NewProduction()
    if err != nil {
            panic(err)
    }
    defer logger.Sync()

    flusher := otel.GetTracerProvider().(otellambda.Flusher)

	lambda.Start(
	    otellambda.InstrumentHandler(handler,
            otellambda.WithFlusher(flusher),
            otellambda.WithEventToCarrier(customEventToCarrier)))
}
