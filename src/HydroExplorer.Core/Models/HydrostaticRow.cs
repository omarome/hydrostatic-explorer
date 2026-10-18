namespace HydroExplorer.Core.Models;

/// <summary>
/// Hydrostatic values for one hull at one draft. Values are unrounded; formatting is the UI's job.
/// </summary>
/// <param name="Draft">Draft T (m).</param>
/// <param name="Volume">Volume of displacement ∇ (m³).</param>
/// <param name="Displacement">Displacement Δ (t).</param>
/// <param name="WaterplaneArea">Waterplane area Awp (m²).</param>
/// <param name="Tpc">Tonnes per centimetre immersion (t/cm).</param>
/// <param name="Kb">Centre of buoyancy above keel (m).</param>
/// <param name="Bm">Transverse metacentric radius (m).</param>
/// <param name="Km">Transverse metacentre above keel (m).</param>
/// <param name="Bml">Longitudinal metacentric radius (m).</param>
/// <param name="Mct">Moment to change trim 1 cm (t·m/cm).</param>
public sealed record HydrostaticRow(
    double Draft,
    double Volume,
    double Displacement,
    double WaterplaneArea,
    double Tpc,
    double Kb,
    double Bm,
    double Km,
    double Bml,
    double Mct);
