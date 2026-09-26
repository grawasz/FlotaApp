namespace Flota.Core.Series;

/// <summary>
/// Redukcja serii danych algorytmem Largest-Triangle-Three-Buckets z zachowaniem kształtu wykresu.
/// </summary>
public static class Lttb
{
    /// <param name="points">Punkty uporządkowane wg osi X.</param>
    /// <param name="threshold">Docelowa liczba punktów (co najmniej 3).</param>
    /// <param name="x">Wartość osi X punktu.</param>
    /// <param name="y">Wartość osi Y punktu.</param>
    public static IReadOnlyList<T> Downsample<T>(IReadOnlyList<T> points, int threshold, Func<T, double> x, Func<T, double> y)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(x);
        ArgumentNullException.ThrowIfNull(y);
        if (threshold < 3 || points.Count <= threshold) return points;

        var result = new List<T>(threshold) { points[0] };
        var bucketSize = (double)(points.Count - 2) / (threshold - 2);
        var a = 0;

        for (var i = 0; i < threshold - 2; i++)
        {
            // Średnia następnego koszyka
            var nextStart = (int)Math.Floor((i + 1) * bucketSize) + 1;
            var nextEnd = Math.Min((int)Math.Floor((i + 2) * bucketSize) + 1, points.Count);
            double avgX = 0, avgY = 0;
            var n = Math.Max(nextEnd - nextStart, 1);
            for (var j = nextStart; j < nextEnd; j++)
            {
                avgX += x(points[j]);
                avgY += y(points[j]);
            }
            avgX /= n;
            avgY /= n;

            // Punkt bieżącego koszyka tworzący największy trójkąt
            var start = (int)Math.Floor(i * bucketSize) + 1;
            var end = (int)Math.Floor((i + 1) * bucketSize) + 1;
            double ax = x(points[a]), ay = y(points[a]);
            var maxArea = -1.0;
            var chosen = start;
            for (var j = start; j < end; j++)
            {
                var area = Math.Abs((ax - avgX) * (y(points[j]) - ay) - (ax - x(points[j])) * (avgY - ay));
                if (area > maxArea)
                {
                    maxArea = area;
                    chosen = j;
                }
            }
            result.Add(points[chosen]);
            a = chosen;
        }

        result.Add(points[^1]);
        return result;
    }
}
