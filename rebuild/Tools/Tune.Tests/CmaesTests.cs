namespace Survival.Tune.Tests;

public class CmaesTests
{
    static double[] Minimise(Func<double[], double> f, double[] start, double sigma, ulong seed, int generations, out Cmaes es)
    {
        es = new Cmaes(start, sigma, seed);
        for (int g = 0; g < generations; g++)
        {
            var xs = es.Ask();
            es.Tell(xs, [.. xs.Select(f)]);
        }
        return es.Mean;
    }

    [Fact]
    public void FindsTheMinimumOfAShiftedEllipsoid()
    {
        double[] target = [0.3, -1.2, 2.5, 0.0, 4.0, -0.7];
        double F(double[] x) => x.Select((v, i) => Math.Pow(10, i / 2.0) * (v - target[i]) * (v - target[i])).Sum();
        var m = Minimise(F, new double[6], 1.0, 7, 400, out _);
        for (int i = 0; i < 6; i++) Assert.Equal(target[i], m[i], 5);
    }

    [Fact]
    public void FindsTheBottomOfRosenbrocksValley()
    {
        static double F(double[] x) => Enumerable.Range(0, x.Length - 1)
            .Sum(i => 100 * Math.Pow(x[i + 1] - x[i] * x[i], 2) + Math.Pow(1 - x[i], 2));
        var m = Minimise(F, [-1, 0, 0.5, -0.5], 0.5, 3, 1500, out _);
        foreach (var v in m) Assert.Equal(1.0, v, 3);
    }

    [Fact]
    public void ToleratesNoise_CloseToTheMinimum()
    {
        // A noisy sphere, as fight counts give: the mean still settles near the optimum.
        var rng = new Rng { State = 99 };
        double F(double[] x) => x.Sum(v => (v - 0.5) * (v - 0.5)) + 0.01 * (rng.NextDouble() - 0.5);
        var m = Minimise(F, [0, 0, 0, 0], 0.3, 11, 200, out _);
        foreach (var v in m) Assert.InRange(v, 0.35, 0.65);
    }

    [Fact]
    public void SameSeed_SameSearch_OtherSeed_Another()
    {
        static double F(double[] x) => x.Sum(v => v * v * (1 + Math.Abs(v)));
        var a = Minimise(F, [1, 2, 3], 0.5, 42, 30, out var ea);
        var b = Minimise(F, [1, 2, 3], 0.5, 42, 30, out var eb);
        var c = Minimise(F, [1, 2, 3], 0.5, 43, 30, out _);
        Assert.Equal(a, b);
        Assert.Equal(ea.Sigma, eb.Sigma);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Lambda_DefaultsToHansens()
    {
        Assert.Equal(4 + (int)Math.Floor(3 * Math.Log(21)), new Cmaes(new double[21], 0.2, 1).Lambda);
        Assert.Equal(6, new Cmaes(new double[21], 0.2, 1, lambda: 6).Lambda);
        Assert.Throws<ArgumentException>(() => new Cmaes(new double[3], 0, 1));
    }

    [Fact]
    public void Eigen_RebuildsTheMatrix()
    {
        double[,] a = { { 4, 1, 0.5 }, { 1, 3, 0.2 }, { 0.5, 0.2, 2 } };
        var (v, roots) = Cmaes.Eigen(a);
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                double sum = 0;
                for (int k = 0; k < 3; k++) sum += v[i, k] * roots[k] * roots[k] * v[j, k];
                Assert.Equal(a[i, j], sum, 9);
            }
    }

    [Fact]
    public void Tell_RefusesAnotherGeneration()
    {
        var es = new Cmaes([0, 0], 1, 1);
        var xs = es.Ask();
        Assert.Throws<ArgumentException>(() => es.Tell(xs[..2], [1, 2]));
    }
}
