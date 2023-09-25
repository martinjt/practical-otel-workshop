using System.Diagnostics;
using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("dotnet-backend"))
    .WithTracing(tpb => 
        tpb
           .AddSource(DiagnosticConfig.Source.Name)
           .AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation()
           .AddOtlpExporter())
    .WithMetrics(mpb => {
        mpb.AddAspNetCoreInstrumentation()
            .AddMeter(DiagnosticConfig.ServiceName)
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddOtlpExporter();
    });

builder.Services.AddHealthChecks();

var app = builder.Build();
var randomAge = new Random();

app.MapGet("/profile", (string firstname, string surname) => {
    using var span = DiagnosticConfig.Source.StartActivity("Generate Age");
    var age = randomAge.Next(18, 100);
    span?.SetTag("age", age);
    span?.Stop();
    DiagnosticConfig.ProfileRequests.Add(1, new("firstname", firstname), new("surname", surname));
    DiagnosticConfig.RequestAge.Record(age, new("firstname", firstname), new("surname", surname));
    return new {
        name = $"{firstname} {surname}",
        age 
    };
});

app.MapHealthChecks("/healthcheck");

app.Run();

static class DiagnosticConfig
{
    public const string ServiceName = "dotnet-backend";
    public static ActivitySource Source = new(ServiceName);
    public static Meter Meter = new(ServiceName, "0.0.1");
    public static Counter<int> ProfileRequests = Meter.CreateCounter<int>("profile_requests", "Number of profile requests");
    public static Histogram<int> RequestAge = Meter.CreateHistogram<int>("request_age", "Average age of profile requests");
}
