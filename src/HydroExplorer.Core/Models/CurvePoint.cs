namespace HydroExplorer.Core.Models;

/// <summary>One point on the hydrostatic curve: displacement at a draft.</summary>
/// <param name="Draft">Draft T (m).</param>
/// <param name="Displacement">Displacement Δ (t).</param>
public sealed record CurvePoint(double Draft, double Displacement);
