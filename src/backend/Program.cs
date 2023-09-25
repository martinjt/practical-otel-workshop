using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("dotnet-backend"))
    .WithTracing(tpb => 
        tpb
           .AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation()
           .AddOtlpExporter())
    .WithMetrics(mpb => {
        mpb.AddAspNetCoreInstrumentation()
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddOtlpExporter();
    });

builder.Services.AddHealthChecks();

var app = builder.Build();
var randomAge = new Random();

app.MapGet("/profile", (string firstname, string surname) => new {
    name = $"{firstname} {surname}",
    age = randomAge.Next(0, 100)
});

app.MapHealthChecks("/healthcheck");

app.Run();
