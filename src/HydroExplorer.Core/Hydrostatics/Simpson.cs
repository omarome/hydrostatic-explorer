namespace HydroExplorer.Core.Hydrostatics;

/// <summary>
/// Composite Simpson's rule: the standard numerical integration in naval architecture ("Simpson's first rule").
/// </summary>
/// <remarks>
/// The interval is split into an even number of equal strips (so an odd number of points), and each pair of
/// strips is fitted with a parabola. That gives the weights 1, 4, 2, 4, …, 2, 4, 1 times spacing / 3.
/// It is exact for polynomials up to degree 3.
/// </remarks>
public static class Simpson
{
    /// <summary>∫ <paramref name="f"/>(x) dx from <paramref name="from"/> to <paramref name="to"/>, sampled at <paramref name="points"/> equally spaced points.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="points"/> is not an odd number of at least 3.</exception>
    public static double Integrate(Func<double, double> f, double from, double to, int points)
    {
        RequireOddPointCount(points, nameof(points));

        double spacing = (to - from) / (points - 1);
        double sum = 0;
        for (int i = 0; i < points; i++)
        {
            // Position from the index, not by adding spacing repeatedly (no floating-point drift).
            sum += Weight(i, points) * f(from + i * spacing);
        }

        return sum * spacing / 3;
    }

    /// <summary>The same rule on values already sampled at equal <paramref name="spacing"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The number of samples is not odd and at least 3.</exception>
    public static double Integrate(IReadOnlyList<double> samples, double spacing)
    {
        RequireOddPointCount(samples.Count, nameof(samples));

        double sum = 0;
        for (int i = 0; i < samples.Count; i++)
        {
            sum += Weight(i, samples.Count) * samples[i];
        }

        return sum * spacing / 3;
    }

    /// <summary>Simpson multiplier for point <paramref name="i"/>: 1 at the ends, then alternately 4 and 2.</summary>
    private static int Weight(int i, int points) => i == 0 || i == points - 1 ? 1 : i % 2 == 1 ? 4 : 2;

    private static void RequireOddPointCount(int points, string name)
    {
        if (points < 3 || points % 2 == 0)
        {
            throw new ArgumentOutOfRangeException(name, points, "Simpson's rule needs an odd number of at least 3 points.");
        }
    }
}
