using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();
builder.Services.AddOpenTelemetry()
    .WithTracing(tpb => 
        tpb.ConfigureResource(resource => resource.AddService("frontend"))
           .AddSource("myapp")
           .AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation()
           .AddConsoleExporter()
           .AddOtlpExporter());

builder.Services.AddSingleton(sp => {
    return sp.GetRequiredService<TracerProvider>().GetTracer("myapp");
});

builder.Services.AddHealthChecks();

var app = builder.Build();

app.MapGet("/", async (HttpClient httpClient, IConfiguration configuration, Tracer tracer,
    string firstname, string surname) => {
        
        // var span = tracer.StartActiveSpan("my internal span");

        // Tracer.CurrentSpan.SetAttribute("firstname", firstname);
        // return $"Hello {firstname}";
        var backendHostname = configuration["BACKEND_HOSTNAME"];
        var result = await httpClient.GetAsync($"http://{backendHostname}/profile?firstname={firstname}&surname={surname}");
        var response = await result.Content.ReadFromJsonAsync<BackendResponse>();
        return $"Hi {response!.name}, you're {response!.age} years old";
});

app.MapHealthChecks("/healthcheck");

app.Run();

record BackendResponse(string name, int age);
