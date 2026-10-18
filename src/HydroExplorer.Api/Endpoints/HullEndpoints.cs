using HydroExplorer.Api.Queries;
using HydroExplorer.Core.Data;
using HydroExplorer.Core.Hydrostatics;
using HydroExplorer.Core.Models;
using HydroExplorer.Core.Paging;

namespace HydroExplorer.Api.Endpoints;

/// <summary>
/// The /api/hulls routes. An extension method on the app keeps Program.cs short
/// (like registering an Express Router).
/// </summary>
public static class HullEndpoints
{
    public const int DefaultCurvePoints = 100;
    public const int MaxCurvePoints = 1000;

    public static void MapHullEndpoints(this IEndpointRouteBuilder app)
    {
        RouteGroupBuilder hulls = app.MapGroup("/api/hulls").WithTags("Hulls");

        hulls.MapGet("/", GetHulls)
            .WithName("GetHulls")
            .WithSummary("Lists all hulls.");

        // {id:int} is a route constraint: a non-integer id doesn't match this route at all (→ 404).
        hulls.MapGet("/{id:int}/hydrostatics", GetHydrostatics)
            .WithName("GetHydrostatics")
            .WithSummary("One page of the hydrostatic table for a hull, with optional sorting and draft filter.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);

        hulls.MapGet("/{id:int}/hydrostatics/curve", GetCurve)
            .WithName("GetHydrostaticCurve")
            .WithSummary("Evenly sampled (draft, displacement) points for charting.")
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound);
    }

    // Handler parameters are filled automatically: "id" from the route, the repository from
    // dependency injection, everything else from the query string. Default values make
    // query parameters optional.

    private static IReadOnlyList<Hull> GetHulls(IHullRepository repository) => repository.GetAll();

    private static IResult GetHydrostatics(
        int id,
        IHullRepository repository,
        int page = 1,
        int pageSize = HydrostaticsQuery.DefaultPageSize,
        string sortBy = "draft",
        string sortDir = "asc",
        double? minDraft = null,
        double? maxDraft = null)
    {
        Hull? hull = repository.GetById(id);
        if (hull is null)
        {
            return HullNotFound(id);
        }

        var query = new HydrostaticsQuery(page, pageSize, sortBy, sortDir, minDraft, maxDraft);
        Dictionary<string, string[]> errors = query.Validate();
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);  // 400 with a ProblemDetails body
        }

        // Generating the table is cheap (≈1,200 rows of arithmetic), so it is done per request.
        IReadOnlyList<HydrostaticRow> rows = HydrostaticTableGenerator.Generate(hull);
        PagedResult<HydrostaticRow> result = query.Apply(rows);
        return Results.Ok(result);
    }

    private static IResult GetCurve(int id, IHullRepository repository, int points = DefaultCurvePoints)
    {
        Hull? hull = repository.GetById(id);
        if (hull is null)
        {
            return HullNotFound(id);
        }

        if (points < 2 || points > MaxCurvePoints)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["points"] = new[] { $"points must be between 2 and {MaxCurvePoints}." },
            });
        }

        return Results.Ok(HydrostaticCurveSampler.Sample(hull, points));
    }

    private static IResult HullNotFound(int id) =>
        Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            title: "Hull not found",
            detail: $"There is no hull with id {id}.");
}
