using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("dotnet-frontend"))
    .WithTracing(tpb => 
        tpb
           .AddSource(DiagnosticConfig.Source.Name)
           .AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation()
           .AddOtlpExporter())
    .WithMetrics(mpb =>
        mpb.AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
            .AddOtlpExporter()
    );

builder.Services.AddSingleton<AlwaysOnSampler>();
builder.Services.AddSingleton(sp => 
    new HealthCheckSampler<AlwaysOnSampler>(10, 
        sp.GetRequiredService<IHttpContextAccessor>(), 
        sp.GetRequiredService<AlwaysOnSampler>()));

var app = builder.Build();

app.MapGet("/", async (HttpClient httpClient, IConfiguration configuration,
    string firstname, string surname) => {
        
        Activity.Current?.SetTag("firstname", firstname);
        Activity.Current?.SetTag("surname", surname);
        using var span = DiagnosticConfig.Source.StartActivity("frontend");
        span?.SetTag("firstname", firstname);
        span?.SetTag("surname", surname);

        var backendHostname = configuration["BACKEND_HOSTNAME"];
        var result = await httpClient.GetAsync($"http://{backendHostname}/profile?firstname={firstname}&surname={surname}");
        var response = await result.Content.ReadFromJsonAsync<BackendResponse>();
        return $"Hi {response!.name}, you're {response!.age} years old";
});

app.MapHealthChecks("/healthcheck");

app.Run();

record BackendResponse(string name, int age);

static class DiagnosticConfig
{
    public static ActivitySource Source = new ("myapp");
}
