using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHealthChecks();
DistributedContextPropagator.Current = DistributedContextPropagator.CreateNoOutputPropagator();
builder.Services.Remove(new ServiceDescriptor(
    typeof(DistributedContextPropagator),
    typeof(DistributedContextPropagator),
    ServiceLifetime.Singleton));
builder.Services.Remove(new ServiceDescriptor(
    typeof(DiagnosticListener),
    typeof(DiagnosticListener),
    ServiceLifetime.Singleton));


builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource
        .AddService("dotnet-frontend")
        .AddHostDetector()
        .AddProcessDetector()
        .AddOperatingSystemDetector())
    .UseOtlpExporter()
    .WithTracing(tpb => 
        tpb
           .AddSource(DiagnosticConfig.Source.Name)
           .AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation()
           .AddProcessor(new SimpleActivityExportProcessor(new SpanLinkExporter()))
           .AddProcessor(new StaticValueProcessor())
           .AddConsoleExporter())
    .WithMetrics(mpb =>
        mpb.AddHttpClientInstrumentation()
            .AddAspNetCoreInstrumentation()
            .AddMeter("Microsoft.AspNetCore.Hosting")
            .AddMeter("Microsoft.AspNetCore.Server.Kestrel")
    )
    .WithLogging();

builder.Services.ConfigureOpenTelemetryTracerProvider((sp, tracerProviderBuilder) =>
{
    tracerProviderBuilder.AddProcessor(new HeadersSpanProcessor(sp.GetRequiredService<IHttpContextAccessor>()));
});

builder.Services.AddSingleton<AlwaysOnSampler>();
builder.Services.AddSingleton(sp => 
    new HealthCheckSampler<AlwaysOnSampler>(10, 
        sp.GetRequiredService<IHttpContextAccessor>(), 
        sp.GetRequiredService<AlwaysOnSampler>()));

var app = builder.Build();

app.MapGet("/", async Task<IResult>(HttpContext context,
    HttpClient httpClient, IConfiguration configuration,
    ILogger<Program> logger,
    [AsParameters]Person person) => {
        if (string.IsNullOrEmpty(person.firstname) &&
            string.IsNullOrEmpty(person.surname))
        {
            Activity.Current?.AddEvent(new ActivityEvent("Missing Fields", tags:
                new ActivityTagsCollection([ 
                    new ("firstname", person.firstname),
                    new ("surname", person.surname)])
                ));
            Activity.Current?.SetStatus(Status.Error);
            return TypedResults.BadRequest("Please provide a firstname and surname");
        }

        logger.LogInformation("Requesting profile for {firstname} {surname}", person.firstname, person.surname, "my-value", 42);

        Baggage.SetBaggage("original_user_agent", context.Request.Headers["User-Agent"].ToString());
        Activity.Current?.AddPerson(person);

        var backendHostname = configuration["BACKEND_HOSTNAME"];
        var result = await httpClient.GetAsync($"http://{backendHostname}/profile?firstname={person.firstname}&surname={person.surname}");
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
class Person
{
    public string firstname { get; set; }
    public string surname { get; set; }    
}

static class ActivityExtensions
{
    public static void AddPerson(this Activity activity, Person person)
    {
        activity.SetTag(DiagnosticNames.PersonFirstname, person.firstname);
        activity.SetTag(DiagnosticNames.PersonSurname, person.surname);
    }
}

static class DiagnosticNames
{
    /// <summary>
    /// The name of the person when the person is the main context of the requests
    /// </summary>
    public const string PersonFirstname = "person.firstname";
    public const string PersonSurname = "person.surname";

}

public class SpanLinkExporter : BaseExporter<Activity>
{
    public override ExportResult Export(in Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            if (string.IsNullOrEmpty(activity.ParentId))
            {
                Console.WriteLine($"{activity.Tags.First(t => t.Key == "url.path").Value} http://localhost:18888/traces/detail/{activity.TraceId}");
            }
        }

        return ExportResult.Success;
    }
}

public class HeadersSpanProcessor : BaseProcessor<Activity>
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HeadersSpanProcessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public override void OnStart(Activity data)
    {
        if (_httpContextAccessor.HttpContext == null)
        {
            return;
        }
        var tenantIdHeader = _httpContextAccessor.HttpContext.Request.Headers["x-tenant-id"];

        var tenantId = tenantIdHeader.Any() ? tenantIdHeader.FirstOrDefault() : "no tenant header";
        data.SetTag("tenant.id", tenantId);
    }
}

public class StaticValueProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity data)
    {
        data.SetTag("static.value", "this is a static value");
    }
}