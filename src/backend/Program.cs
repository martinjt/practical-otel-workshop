var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var randomAge = new Random();

app.MapGet("/profile", (string firstname, string surname) => new {
    name = $"{firstname} {surname}",
    age = randomAge.Next(0, 100)
});

app.Run();
