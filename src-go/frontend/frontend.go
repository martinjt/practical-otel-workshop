package main

import (
	"context"
	"encoding/json"
	"fmt"
	"go.opentelemetry.io/contrib/instrumentation/net/http/otelhttp"
	"go.opentelemetry.io/otel"
	"go.opentelemetry.io/otel/exporters/otlp/otlptrace/otlptracegrpc"
	"go.opentelemetry.io/otel/propagation"
	"go.opentelemetry.io/otel/sdk/resource"
	"go.opentelemetry.io/otel/sdk/trace"
	semconv "go.opentelemetry.io/otel/semconv/v1.20.0"
	"log"
	"math/rand"
	"net/http"
	"os"
)

func main() {
	l := log.New(os.Stdout, "", 0)
	ctx := context.Background()
	grpcExporter, err := otlptracegrpc.New(ctx, otlptracegrpc.WithInsecure())
	exp := NewSpanLinkExporter()

	if err != nil {
		log.Fatalf("%s: %v", "failed to create exporter", err)
	}

	appResource, err := resource.New(ctx,
		resource.WithAttributes(
			semconv.ServiceNameKey.String("go-frontend"),
			semconv.ServiceVersionKey.String("1.0.1"),
			semconv.TelemetrySDKLanguageGo,
		),
	)

	tp := trace.NewTracerProvider(
		trace.WithResource(appResource),
		trace.WithBatcher(exp),
		trace.WithBatcher(grpcExporter),
	)

	defer func() {
		if err := tp.Shutdown(context.Background()); err != nil {
			l.Fatal(err)
		}
	}()
	otel.SetTracerProvider(tp)
	otel.SetTextMapPropagator(
		propagation.NewCompositeTextMapPropagator(
			propagation.Baggage{},
			propagation.TraceContext{},
		),
	)

	// Set up http mux and configure endpoint
	mux := http.NewServeMux()
	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		data, _ := json.Marshal(map[string]interface{}{
			"name": "name", "age": rand.Intn(100),
		})
		fmt.Fprintf(w, string(data))
	})

	// setup automatic instrumentation of mux
	wrappedHandler := otelhttp.NewHandler(mux, "API")

	log.Println("Listening on http://localhost:5012/")
	log.Fatal(http.ListenAndServe(":5012", wrappedHandler))
}

type spanLinkExporter struct {
	linkUrl string
}

func (e *spanLinkExporter) Shutdown(ctx context.Context) error {
	return nil
}

func NewSpanLinkExporter() *spanLinkExporter {
	return &spanLinkExporter{
		linkUrl: "http://localhost:16686/trace/",
	}
}

func (e *spanLinkExporter) ExportSpans(ctx context.Context, spans []trace.ReadOnlySpan) error {
	if len(spans) == 0 {
		return nil
	}

	for _, span := range spans {
		// if a root span (ie no parent span ID)
		if !span.Parent().SpanID().IsValid() {
			fmt.Printf("Trace for %s\nJaeger link: %s%s\n", span.Name(), e.linkUrl, span.SpanContext().TraceID().String())
		}
	}

	return nil
}
