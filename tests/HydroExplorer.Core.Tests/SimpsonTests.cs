using HydroExplorer.Core.Hydrostatics;

namespace HydroExplorer.Core.Tests;

public class SimpsonTests
{
    [Fact]
    public void Integrate_Cubic_IsExact()
    {
        // Simpson's rule is exact for polynomials up to degree 3: ∫₀² (x³ − 2x + 1) dx = 4 − 4 + 2 = 2.
        double result = Simpson.Integrate(x => x * x * x - 2 * x + 1, 0, 2, points: 3);

        Assert.Equal(2.0, result, 12);
    }

    [Fact]
    public void Integrate_Sine_IsAccurate()
    {
        // ∫₀^π sin x dx = 2. With 41 points the error is about 1e-6.
        double result = Simpson.Integrate(Math.Sin, 0, Math.PI, points: 41);

        Assert.Equal(2.0, result, 5);
    }

    [Fact]
    public void Integrate_FromSamples_MatchesTheFunctionOverload()
    {
        double[] samples = Enumerable.Range(0, 5).Select(i => Math.Exp(i * 0.25)).ToArray();

        double fromSamples = Simpson.Integrate(samples, spacing: 0.25);
        double fromFunction = Simpson.Integrate(Math.Exp, 0, 1, points: 5);

        Assert.Equal(fromFunction, fromSamples, 14);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    public void Integrate_NeedsAnOddNumberOfAtLeast3Points(int points)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Simpson.Integrate(x => x, 0, 1, points));
    }
}
