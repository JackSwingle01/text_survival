namespace Survival.Tune;

/// CMA-ES, the covariance matrix adaptation evolution strategy, as in Hansen's tutorial
/// ("The CMA Evolution Strategy: A Tutorial", 2016), with its default constants and no restarts.
/// Ask for a generation, score it, tell the scores back (lower is better). Deterministic given the
/// seed: the only randomness is its own SplitMix stream.
public sealed class Cmaes
{
    readonly int n;
    readonly double[] weights;
    readonly double muEff, cc, cs, c1, cmu, damps, chiN;
    readonly Rng rng;
    readonly double[] pc, ps;
    double[,] c, b; // covariance, and its eigenvectors as columns
    double[] d;     // square roots of its eigenvalues
    double[][] zs = [];

    public int Lambda { get; }
    public int Mu { get; }
    public double[] Mean { get; }
    public double Sigma { get; private set; }
    public int Generation { get; private set; }
    /// The covariance's largest over smallest axis: a search that has learned a valley stretches this.
    public double AxisRatio => d.Max() / Math.Max(1e-300, d.Min());

    public Cmaes(IReadOnlyList<double> mean, double sigma, ulong seed, int? lambda = null)
    {
        n = mean.Count;
        if (n < 1 || !(sigma > 0)) throw new ArgumentException("CMA-ES needs a dimension and a positive step");
        Mean = [.. mean];
        Sigma = sigma;
        rng = new Rng { State = seed };
        Lambda = lambda ?? 4 + (int)Math.Floor(3 * Math.Log(n));
        if (Lambda < 2) throw new ArgumentException("lambda must be at least 2");
        Mu = Lambda / 2;
        var raw = Enumerable.Range(1, Mu).Select(i => Math.Log(Mu + 0.5) - Math.Log(i)).ToArray();
        double sum = raw.Sum();
        weights = [.. raw.Select(w => w / sum)];
        muEff = 1 / weights.Sum(w => w * w);
        cc = (4 + muEff / n) / (n + 4 + 2 * muEff / n);
        cs = (muEff + 2) / (n + muEff + 5);
        c1 = 2 / ((n + 1.3) * (n + 1.3) + muEff);
        cmu = Math.Min(1 - c1, 2 * (muEff - 2 + 1 / muEff) / ((n + 2) * (n + 2) + muEff));
        damps = 1 + 2 * Math.Max(0, Math.Sqrt((muEff - 1) / (n + 1)) - 1) + cs;
        chiN = Math.Sqrt(n) * (1 - 1.0 / (4 * n) + 1.0 / (21.0 * n * n));
        pc = new double[n];
        ps = new double[n];
        c = Identity(n);
        b = Identity(n);
        d = [.. Enumerable.Repeat(1.0, n)];
    }

    static double[,] Identity(int n)
    {
        var m = new double[n, n];
        for (int i = 0; i < n; i++) m[i, i] = 1;
        return m;
    }

    double Normal()
    {
        // Box-Muller; 1 - u keeps the log away from 0.
        double u1 = 1 - rng.NextDouble(), u2 = rng.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }

    /// This generation's Lambda candidates: mean + sigma * B D z.
    public double[][] Ask()
    {
        zs = [.. Enumerable.Range(0, Lambda).Select(_ => Enumerable.Range(0, n).Select(_ => Normal()).ToArray())];
        return [.. zs.Select(z =>
        {
            var x = new double[n];
            for (int i = 0; i < n; i++)
            {
                double y = 0;
                for (int j = 0; j < n; j++) y += b[i, j] * d[j] * z[j];
                x[i] = Mean[i] + Sigma * y;
            }
            return x;
        })];
    }

    /// The scores of the candidates the last Ask returned, in the same order.
    public void Tell(double[][] xs, IReadOnlyList<double> fitness)
    {
        if (xs.Length != Lambda || fitness.Count != Lambda || zs.Length != Lambda) throw new ArgumentException("Tell takes the generation Ask gave");
        // Ties keep candidate order, so the update is a function of the scores alone.
        var order = Enumerable.Range(0, Lambda).OrderBy(i => fitness[i]).ThenBy(i => i).Take(Mu).ToArray();
        var old = (double[])Mean.Clone();
        var ys = order.Select(k => Enumerable.Range(0, n).Select(i => (xs[k][i] - old[i]) / Sigma).ToArray()).ToArray();
        var yw = new double[n];
        var zw = new double[n];
        for (int k = 0; k < Mu; k++)
            for (int i = 0; i < n; i++) { yw[i] += weights[k] * ys[k][i]; zw[i] += weights[k] * zs[order[k]][i]; }
        for (int i = 0; i < n; i++) Mean[i] = old[i] + Sigma * yw[i];

        // C^-1/2 yw = B zw: z was drawn before B and D scaled it.
        double csFactor = Math.Sqrt(cs * (2 - cs) * muEff);
        for (int i = 0; i < n; i++)
        {
            double bz = 0;
            for (int j = 0; j < n; j++) bz += b[i, j] * zw[j];
            ps[i] = (1 - cs) * ps[i] + csFactor * bz;
        }
        double psNorm = Math.Sqrt(ps.Sum(v => v * v));
        bool hsig = psNorm / Math.Sqrt(1 - Math.Pow(1 - cs, 2 * (Generation + 1))) / chiN < 1.4 + 2.0 / (n + 1);
        double ccFactor = Math.Sqrt(cc * (2 - cc) * muEff);
        for (int i = 0; i < n; i++) pc[i] = (1 - cc) * pc[i] + (hsig ? ccFactor * yw[i] : 0);

        double keep = 1 - c1 - cmu + (hsig ? 0 : c1 * cc * (2 - cc));
        var next = new double[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j <= i; j++)
            {
                double rankMu = 0;
                for (int k = 0; k < Mu; k++) rankMu += weights[k] * ys[k][i] * ys[k][j];
                next[i, j] = next[j, i] = keep * c[i, j] + c1 * pc[i] * pc[j] + cmu * rankMu;
            }
        c = next;
        Sigma *= Math.Exp(cs / damps * (psNorm / chiN - 1));
        (b, d) = Eigen(c);
        Generation++;
    }

    /// Symmetric eigendecomposition by cyclic Jacobi rotations: eigenvectors as columns, and the
    /// square roots of the eigenvalues (floored, so a collapsed axis never divides by zero).
    public static (double[,] Vectors, double[] Roots) Eigen(double[,] matrix)
    {
        int n = matrix.GetLength(0);
        var a = (double[,])matrix.Clone();
        var v = Identity(n);
        for (int sweep = 0; sweep < 100; sweep++)
        {
            double off = 0;
            for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++) off += a[i, j] * a[i, j];
            if (off < 1e-30) break;
            for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-300) continue;
                    double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    double t = Math.Sign(theta == 0 ? 1 : theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    double cos = 1 / Math.Sqrt(t * t + 1), sin = t * cos;
                    for (int k = 0; k < n; k++)
                    {
                        double akp = a[k, p], akq = a[k, q];
                        a[k, p] = cos * akp - sin * akq;
                        a[k, q] = sin * akp + cos * akq;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double apk = a[p, k], aqk = a[q, k];
                        a[p, k] = cos * apk - sin * aqk;
                        a[q, k] = sin * apk + cos * aqk;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = cos * vkp - sin * vkq;
                        v[k, q] = sin * vkp + cos * vkq;
                    }
                }
        }
        return (v, [.. Enumerable.Range(0, n).Select(i => Math.Sqrt(Math.Max(a[i, i], 1e-20)))]);
    }
}
