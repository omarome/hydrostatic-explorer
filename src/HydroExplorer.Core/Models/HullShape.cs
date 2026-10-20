namespace HydroExplorer.Core.Models;

/// <summary>
/// The shape of a ship hull, as three exponents of an analytic hull form. The half-breadth is
/// <c>y(x, z) = B/2 · f(ξ) · g(z)</c>, where
/// <c>f(ξ) = 1 − |ξ|^p</c> (waterline shape, p = bow or stern exponent) and
/// <c>g(z) = 1 − (1 − z/D)^q</c> (section shape, q = section exponent); see <see cref="Hydrostatics.HullGeometry"/>.
/// </summary>
/// <remarks>
/// p = q = 2 is the classic Wigley hull. Bigger exponents mean fuller ends (a longer parallel midbody) and
/// flatter, more vertical sections; as they grow the shape tends to a box.
/// </remarks>
/// <param name="BowExponent">p for the forward half (bigger → fuller bow).</param>
/// <param name="SternExponent">p for the aft half (bigger → fuller stern).</param>
/// <param name="SectionExponent">q (bigger → flatter bottom, more vertical sides).</param>
/// <param name="Superstructure">Where the superstructure stands (drawings only). Optional, so older JSON still reads.</param>
public sealed record HullShape(
    double BowExponent,
    double SternExponent,
    double SectionExponent,
    SuperstructureLayout Superstructure = SuperstructureLayout.AftAccommodation)
{
    /// <summary>
    /// Block coefficient Cb = ∇ / (L·B·D) with the hull immersed to the deck. Because the half-breadth is a product
    /// f(ξ)·g(z), Cb is mean(f) · mean(g), and the mean of 1 − s^n over [0, 1] is n / (n + 1).
    /// </summary>
    public double FullDepthBlockCoefficient =>
        0.5 * (BowExponent / (BowExponent + 1) + SternExponent / (SternExponent + 1))
        * (SectionExponent / (SectionExponent + 1));
}
