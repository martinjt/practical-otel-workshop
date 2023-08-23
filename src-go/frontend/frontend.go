package main

import (
	"encoding/json"
	"fmt"
	"log"
	"math/rand"
	"net/http"

	"github.com/honeycombio/otel-config-go/otelconfig"
	"go.opentelemetry.io/contrib/instrumentation/net/http/otelhttp"
)

func main() {
	otelShutdown, err := otelconfig.ConfigureOpenTelemetry()
	if err != nil {
		log.Fatalf("error configuring OTel SDK - %e", err)
	}
	defer otelShutdown()

	// Set up http mux and configure endpoint
	mux := http.NewServeMux()
	mux.HandleFunc("/", func(w http.ResponseWriter, r *http.Request) {
		data, _ := json.Marshal(map[string]interface{}{
			"name": "name", "age": rand.Intn(100),
		})
		fmt.Fprintf(w, string(data))
	})

	// setup automatic instrumentation of mux
	wrappedHandler := otelhttp.NewHandler(mux, "year")

	log.Println("Listening on http://localhost:8080/")
	log.Fatal(http.ListenAndServe(":8080", wrappedHandler))
}
