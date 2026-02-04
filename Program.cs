using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// // Configure Docker
// builder.WebHost.ConfigureKestrel(options =>
// {
//     options.ListenAnyIP(8080); // Docker standard port
// });

builder.Services.AddOpenApi();
builder.Services.AddAntiforgery();
builder.Configuration.AddEnvironmentVariables();

// Add CORS for web use
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb", policy =>
    {
        policy.WithOrigins(builder.Configuration["AllowedOrigins"] ?? "*")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

var apiKey = builder.Configuration["HuggingFace:ApiKey"];
if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("Missing HuggingFace:ApiKey environment variable.");
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AllowWeb");
app.UseHttpsRedirection(); // Disable for Docker
app.UseAntiforgery();

app.MapPost("/process-image", async (IFormFile image, string prompt = "") =>
{
    if (image == null || image.Length == 0)
        return Results.BadRequest("No image provided");

    if (image.Length > 10 * 1024 * 1024) // 10MB limit
        return Results.BadRequest("Image too large");

    // Check image format to be compatable with the LLM
    if (!new[] { "image/png", "image/jpeg", "image/jpg" }.Contains(image.ContentType))
        return Results.BadRequest("Invalid image format. Only PNG and JPEG are supported.");

    // Process image
    using var stream = image.OpenReadStream();
    using var memoryStream = new MemoryStream();
    await stream.CopyToAsync(memoryStream);
    var imageBytes = memoryStream.ToArray();
    var base64Image = Convert.ToBase64String(imageBytes);
    var dataUrl = $"data:{image.ContentType};base64,{base64Image}";

    using var client = new HttpClient
    {
        BaseAddress = new Uri("https://router.huggingface.co/v1/chat/completions")
    };
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

    var requestBody = new
    {
        messages = new[]
        {
            new
            {
                role = "user",
                content = new object[]
                {
                    new { type = "text", text = prompt },
                    new { type = "image_url", image_url = new { url = dataUrl } }
                }
            }
        },
        model = "moonshotai/Kimi-K2.5:fireworks-ai",
        stream = false
    };

    var jsonContent = new StringContent(
        JsonSerializer.Serialize(requestBody),
        System.Text.Encoding.UTF8,
        "application/json"
    );

    try
    {
        var response = await client.PostAsync("", jsonContent);
        response.EnsureSuccessStatusCode();
        var responseBody = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseBody);
        var messageContent = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        return Results.Ok(new { description = messageContent });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error processing image: {ex.Message}");
    }
})
.DisableAntiforgery()
.WithName("POSTimage");

app.Run();

