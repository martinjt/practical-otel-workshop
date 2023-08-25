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

	if err != nil {
		log.Fatalf("%s: %v", "failed to create exporter", err)
	}

	appResource, err := resource.New(ctx,
		resource.WithAttributes(
			semconv.ServiceNameKey.String("go-backend"),
			semconv.ServiceVersionKey.String("1.0.1"),
			semconv.TelemetrySDKLanguageGo,
		),
	)

	tp := trace.NewTracerProvider(
		trace.WithResource(appResource),
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
		firstname := r.URL.Query().Get("firstname")
		surname := r.URL.Query().Get("surname")
		data, _ := json.Marshal(map[string]interface{}{
			"name": fmt.Sprintf("%s %s", firstname, surname),
			"age":  rand.Intn(100),
		})
		fmt.Fprintf(w, string(data))
	})

	// setup automatic instrumentation of mux
	wrappedHandler := otelhttp.NewHandler(mux, "API")

	log.Println("Listening on http://localhost:5013/")
	log.Fatal(http.ListenAndServe(":5013", wrappedHandler))
}
