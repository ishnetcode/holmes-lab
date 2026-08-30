using Prometheus;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Fake in-memory "database" of orders
var orders = new Dictionary<int, string>
{
    { 1, "Widget" },
    { 2, "Gadget" },
    { 3, "Gizmo" }
};

// Tracks request count + duration automatically (http_request_duration_seconds, etc.)
app.UseHttpMetrics();

// Exposes GET /metrics for Prometheus to scrape
app.MapMetrics();

// A custom counter, since we want to track failures specifically
var failuresTotal = Metrics.CreateCounter(
    "http_requests_failed_total",
    "Total number of requests that resulted in a simulated failure");

// Used by Kubernetes to know the pod is alive
app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

app.MapGet("/api/orders", (ILogger<Program> logger) =>
{
    logger.LogInformation("All orders requested");
    return Results.Ok(orders);
});

app.MapGet("/api/orders/{id:int}", (int id, ILogger<Program> logger) =>
{
    logger.LogInformation("Order {OrderId} requested", id);

    if (orders.TryGetValue(id, out var name))
        return Results.Ok(new { id, name });

    logger.LogWarning("Order {OrderId} not found", id);
    return Results.NotFound();
});

// Deliberately broken: always returns HTTP 500
app.MapGet("/api/failure", (ILogger<Program> logger) =>
{
    logger.LogError("Simulated failure: database connection failed");
    failuresTotal.Inc();
    return Results.Problem("Database connection failed", statusCode: 500);
});

// Deliberately slow: always takes 5 seconds
app.MapGet("/api/slow", async (ILogger<Program> logger) =>
{
    logger.LogWarning("Order processing taking longer than expected");
    await Task.Delay(5000);
    return Results.Ok(new { message = "That took a while, on purpose" });
});

app.Run();
