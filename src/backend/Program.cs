using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenTelemetry()
    .WithTracing(tpb => 
        tpb.ConfigureResource(resource => resource.AddService("backend"))
           .AddAspNetCoreInstrumentation()
           .AddHttpClientInstrumentation()
           .AddConsoleExporter());
var app = builder.Build();
var randomAge = new Random();

app.MapGet("/profile", (string firstname, string surname) => new {
    name = $"{firstname} {surname}",
    age = randomAge.Next(0, 100)
});

app.Run();
