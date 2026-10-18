var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// Placeholder health endpoint; the real endpoints arrive in Phase 2.
app.MapGet("/", () => "Hydrostatics Explorer API");

app.Run();
