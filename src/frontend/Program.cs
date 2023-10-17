using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OpenTelemetry;
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

app.MapGet("/", async Task<IResult>(HttpContext context, 
    HttpClient httpClient, IConfiguration configuration,
    [AsParameters]Person person) => {

        if (string.IsNullOrEmpty(person.firstname) &&
            string.IsNullOrEmpty(person.surname))
        {
            Activity.Current?.AddEvent(new ActivityEvent("Missing Fields", tags:
                new ActivityTagsCollection(new KeyValuePair<string, object?>[] { 
                    new ("firstname", person.firstname),
                    new ("surname", person.surname)})
                ));
            Activity.Current?.SetStatus(Status.Error);
            return TypedResults.BadRequest("Please provide a firstname and surname");
        }

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