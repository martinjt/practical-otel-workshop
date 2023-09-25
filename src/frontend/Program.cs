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

app.MapGet("/", async Task<IResult>(HttpClient httpClient, IConfiguration configuration,
    string firstname, string surname) => {

        if (string.IsNullOrEmpty(firstname) &&
            string.IsNullOrEmpty(surname))
        {
            Activity.Current?.AddEvent(new ActivityEvent("Missing Fields", tags:
                new ActivityTagsCollection(new KeyValuePair<string, object?>[] { 
                    new ("firstname", firstname),
                    new ("surname", surname)})
                ));
            Activity.Current?.SetStatus(Status.Error);
            return TypedResults.BadRequest("Please provide a firstname and surname");
        }
        Activity.Current?.SetTag("firstname", firstname);
        Activity.Current?.SetTag("surname", surname);

        var backendHostname = configuration["BACKEND_HOSTNAME"];
        var result = await httpClient.GetAsync($"http://{backendHostname}/profile?firstname={firstname}&surname={surname}");
        var response = await result.Content.ReadFromJsonAsync<BackendResponse>();
        return TypedResults.Ok($"Hi {response!.name}, you're {response!.age} years old");
});

app.MapHealthChecks("/healthcheck");

app.Run();

record BackendResponse(string name, int age);

static class DiagnosticConfig
{
    public static ActivitySource Source = new ("myapp");
}
