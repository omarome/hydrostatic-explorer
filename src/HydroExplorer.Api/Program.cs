using HydroExplorer.Api.Endpoints;
using HydroExplorer.Api.Infrastructure;
using HydroExplorer.Core.Data;

var builder = WebApplication.CreateBuilder(args);

// ---- Services (dependency injection registrations) ----
// Singleton: one shared instance for the app's lifetime. The seed repository is read-only, so sharing is safe.
builder.Services.AddSingleton<IHullRepository, SeedHullRepository>();
builder.Services.Configure<SimulatedLatencyOptions>(
    builder.Configuration.GetSection(SimulatedLatencyOptions.SectionName));

// Errors (404s, unhandled exceptions, empty 400s) are returned as RFC 7807 ProblemDetails JSON.
builder.Services.AddProblemDetails();

// A query value that can't be parsed (e.g. ?page=abc) should be a 400. In Development, minimal APIs
// throw an exception for it instead, which the exception handler would turn into a 500.
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = false);

// Swagger / OpenAPI document generation.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---- Middleware pipeline (runs in this order for every request) ----
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();    // /swagger/v1/swagger.json
    app.UseSwaggerUI();  // /swagger
}

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseMiddleware<SimulatedLatencyMiddleware>();

// ---- Endpoints ----
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapHullEndpoints();

app.Run();

/// <summary>
/// Makes the compiler-generated Program class public, so the test project's
/// WebApplicationFactory&lt;Program&gt; can start this API in memory.
/// </summary>
public partial class Program
{
}
