using System;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddAntiforgery();
builder.Configuration.AddEnvironmentVariables();

var app = builder.Build();

var apiKey = builder.Configuration["HuggingFace:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Missing HuggingFace:ApiKey environment variable.");
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
// app.UseAntiforgery();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast");

app.MapPost("/process-image", async (IFormFile image, string prompt = "") =>
{
    if (image == null || image.Length == 0)
        return Results.BadRequest("No image provided");

    if (image.Length > 10 * 1024 * 1024) // 10MB limit
        return Results.BadRequest("Image too large");

    // Validate MIME type
    if (!image.ContentType.StartsWith("image/"))
        return Results.BadRequest("Invalid image format");

    // Process image (e.g., save to temp, run ML model, etc.)
    using var stream = image.OpenReadStream();
    // Your image logic here...
    HttpClient client = new();
    // HttpContent content = new 
    client.BaseAddress = new Uri("https://router.huggingface.co/hf-inference/models/google/vit-base-patch16-224");
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

    return Results.Ok(new { result = $"{apiKey}!" });
})
.DisableAntiforgery()
.WithName("POSTimage");

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
