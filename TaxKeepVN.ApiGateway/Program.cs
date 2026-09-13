var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Correlation ID tracing middleware
app.Use(async (context, next) =>
{
    const string correlationHeader = "X-Correlation-Id";
    if (!context.Request.Headers.TryGetValue(correlationHeader, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString();
        context.Request.Headers[correlationHeader] = correlationId;
    }
    context.Response.Headers[correlationHeader] = correlationId;
    await next();
});

app.UseCors("AllowAll");

// Health check endpoint for monitoring Gateway uptime
app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "TaxKeepVN.ApiGateway",
    timestamp = DateTime.UtcNow
}));

// Configure the HTTP request pipeline.
app.UseSwaggerUI(c =>
{
        c.SwaggerEndpoint("/taxkeep/swagger/v1/swagger.json", "TaxKeepVN Management API");
    c.SwaggerEndpoint("/ai/openapi.json", "Tax AI Service API (FastAPI)");
    c.RoutePrefix = "swagger"; // Set Swagger UI at /swagger
});

// app.UseHttpsRedirection();

app.MapReverseProxy();

app.Run();
