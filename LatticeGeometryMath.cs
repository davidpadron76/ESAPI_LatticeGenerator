using System;
using System.Collections.Generic;
using System.Linq;

namespace VMS.TPS.LatticeMath
{
    // Pure geometry/math helpers for the LATTICE hot/cold checkerboard engine.
    // No dependency on VMS.TPS.Common.Model.* or WPF, so this file can be
    // linked into a standalone test project without any Varian DLLs.

    public struct Vec3
    {
        public readonly double X, Y, Z;

        public Vec3(double x, double y, double z)
        {
            X = x; Y = y; Z = z;
        }

        public static Vec3 operator +(Vec3 a, Vec3 b) => new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static Vec3 operator -(Vec3 a, Vec3 b) => new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static Vec3 operator *(Vec3 a, double s) => new Vec3(a.X * s, a.Y * s, a.Z * s);

        public double LengthSquared => X * X + Y * Y + Z * Z;
    }

    // Grid offset (mm) applied before the tilt rotation, plus which checkerboard
    // parity (0 or 1) is treated as "hot" for this configuration.
    public struct PhaseOffset
    {
        public readonly double Dx, Dy, Dz;
        public readonly int HotParity;

        public PhaseOffset(double dx, double dy, double dz, int hotParity)
        {
            Dx = dx; Dy = dy; Dz = dz; HotParity = hotParity;
        }
    }

    public struct ConfigScore
    {
        public readonly int HotConsidered, HotAccepted, ColdConsidered, ColdAccepted;
        public readonly double HotRatioPercent;

        public ConfigScore(int hotConsidered, int hotAccepted, int coldConsidered, int coldAccepted, double hotRatioPercent)
        {
            HotConsidered = hotConsidered;
            HotAccepted = hotAccepted;
            ColdConsidered = coldConsidered;
            ColdAccepted = coldAccepted;
            HotRatioPercent = hotRatioPercent;
        }

        public int TotalAccepted => HotAccepted + ColdAccepted;
    }

    public sealed class GridCandidate
    {
        public readonly int I, J, K;
        public readonly Vec3 Position; // absolute patient-space point (com + tilted offset)
        public readonly bool IsHot;

        public GridCandidate(int i, int j, int k, Vec3 position, bool isHot)
        {
            I = i; J = j; K = k; Position = position; IsHot = isHot;
        }
    }

    public sealed class GridBuildResult
    {
        public readonly List<GridCandidate> AcceptedHot = new List<GridCandidate>();
        public readonly List<GridCandidate> AcceptedCold = new List<GridCandidate>();
        public int HotConsidered;
        public int ColdConsidered;
    }

    public static class HaltonSequence
    {
        // Standard radical-inverse (van der Corput) function in the given base.
        public static double Value(int index, int baseN)
        {
            double result = 0.0;
            double f = 1.0 / baseN;
            int i = index;
            while (i > 0)
            {
                result += f * (i % baseN);
                i /= baseN;
                f /= baseN;
            }
            return result;
        }

        // Deterministic low-discrepancy points inside the unit sphere (bases 2,3,5).
        // Keeps drawing triplets until exactly targetCount land inside the sphere,
        // so targetCount is always the honest denominator for overlap fractions.
        public static IReadOnlyList<Vec3> GenerateUnitSphereSamples(int targetCount = 513)
        {
            var samples = new List<Vec3>(targetCount);
            int index = 1;
            while (samples.Count < targetCount)
            {
                double x = 2.0 * Value(index, 2) - 1.0;
                double y = 2.0 * Value(index, 3) - 1.0;
                double z = 2.0 * Value(index, 5) - 1.0;
                index++;

                if (x * x + y * y + z * z <= 1.0)
                {
                    samples.Add(new Vec3(x, y, z));
                }
            }
            return samples;
        }
    }

    public static class SphereOverlapSampler
    {
        public static int CountInside(Vec3 center, double radiusMm, IReadOnlyList<Vec3> unitSamples, Func<Vec3, bool> isInsideRegion)
        {
            int count = 0;
            for (int i = 0; i < unitSamples.Count; i++)
            {
                Vec3 pt = center + unitSamples[i] * radiusMm;
                if (isInsideRegion(pt))
                {
                    count++;
                }
            }
            return count;
        }

        public static bool PassesThreshold(int insideCount, int totalSamples, double minimumFraction = 0.5, double epsilon = 1e-9)
        {
            int required = (int)Math.Ceiling(minimumFraction * totalSamples - epsilon);
            return insideCount >= required;
        }

        // Cheap 6-point axis-extremal test, used to skip the full 513-sample pass
        // for candidates that are obviously fully inside or fully outside.
        // Returns true=accept, false=reject, null=ambiguous (needs full sampling).
        public static bool? ExtremalPreCheck(Vec3 center, double radiusMm, Func<Vec3, bool> isInsideRegion)
        {
            Vec3[] probes =
            {
                new Vec3(center.X + radiusMm, center.Y, center.Z),
                new Vec3(center.X - radiusMm, center.Y, center.Z),
                new Vec3(center.X, center.Y + radiusMm, center.Z),
                new Vec3(center.X, center.Y - radiusMm, center.Z),
                new Vec3(center.X, center.Y, center.Z + radiusMm),
                new Vec3(center.X, center.Y, center.Z - radiusMm),
            };

            int insideCount = probes.Count(isInsideRegion);
            if (insideCount == probes.Length) return true;
            if (insideCount == 0) return false;
            return null;
        }
    }

    public static class PhaseSelector
    {
        public static IEnumerable<PhaseOffset> EnumerateConfigurations(double separationMm)
        {
            double half = separationMm / 2.0;
            double[] offsets = { 0.0, half };

            foreach (var dx in offsets)
                foreach (var dy in offsets)
                    foreach (var dz in offsets)
                        for (int hotParity = 0; hotParity <= 1; hotParity++)
                            yield return new PhaseOffset(dx, dy, dz, hotParity);
        }

        // Ranks configurations by: (1) distance from the [bandMin,bandMax] ratio
        // band, ascending (0 = inside band); (2) total accepted spheres,
        // descending; (3) closeness to the band's low end, ascending (tie-break
        // bias toward the more conservative/lower ratio).
        public static int SelectBestConfigurationIndex(IReadOnlyList<ConfigScore> scores, double bandMinPercent = 2.0, double bandMaxPercent = 4.0)
        {
            if (scores == null || scores.Count == 0)
            {
                throw new ArgumentException("scores must be non-empty", "scores");
            }

            return Enumerable.Range(0, scores.Count)
                .OrderBy(i => DistanceFromBand(scores[i].HotRatioPercent, bandMinPercent, bandMaxPercent))
                .ThenByDescending(i => scores[i].TotalAccepted)
                .ThenBy(i => Math.Abs(scores[i].HotRatioPercent - bandMinPercent))
                .First();
        }

        private static double DistanceFromBand(double ratio, double bandMinPercent, double bandMaxPercent)
        {
            if (ratio < bandMinPercent) return bandMinPercent - ratio;
            if (ratio > bandMaxPercent) return ratio - bandMaxPercent;
            return 0.0;
        }
    }

    public static class GridBuilder
    {
        // Compone dos rotaciones rígidas: primero alrededor del eje Z (mezcla
        // X/Y, reparte la malla entre planos sagitales), luego alrededor del eje
        // X (mezcla Y/Z, reparte la malla entre cortes axiales). La composición
        // de dos rotaciones ortogonales sigue siendo una transformación rígida
        // (conserva distancias), sin importar el orden en que se apliquen.
        public static GridBuildResult BuildCandidateGrid(
            Vec3 com,
            double separationMm,
            int n,
            double tiltAxialRad,
            double tiltSagittalRad,
            PhaseOffset phase,
            Func<Vec3, bool> isInsideHotRegion,
            Func<Vec3, bool> isInsideColdEnvelope,
            Func<Vec3, bool> bboxPreFilter = null)
        {
            var result = new GridBuildResult();
            double cosAxial = Math.Cos(tiltAxialRad);
            double sinAxial = Math.Sin(tiltAxialRad);
            double cosSagittal = Math.Cos(tiltSagittalRad);
            double sinSagittal = Math.Sin(tiltSagittalRad);

            for (int i = -n; i <= n; i++)
            {
                double xLocal = i * separationMm + phase.Dx;
                for (int j = -n; j <= n; j++)
                {
                    double yLocal = j * separationMm + phase.Dy;
                    for (int k = -n; k <= n; k++)
                    {
                        double zLocal = k * separationMm + phase.Dz;

                        // Paso 1: rotación alrededor de Z (mezcla X e Y).
                        double xRotZ = xLocal * cosSagittal - yLocal * sinSagittal;
                        double yRotZ = xLocal * sinSagittal + yLocal * cosSagittal;

                        // Paso 2: rotación alrededor de X (mezcla Y y Z), sobre el resultado del paso 1.
                        double yRot = yRotZ * cosAxial - zLocal * sinAxial;
                        double zRot = yRotZ * sinAxial + zLocal * cosAxial;

                        Vec3 pt = new Vec3(com.X + xRotZ, com.Y + yRot, com.Z + zRot);

                        if (bboxPreFilter != null && !bboxPreFilter(pt))
                        {
                            continue;
                        }

                        int parity = ((i + j + k) % 2 + 2) % 2;
                        bool isHot = parity == phase.HotParity;

                        if (isHot)
                        {
                            result.HotConsidered++;
                            if (isInsideHotRegion(pt))
                            {
                                result.AcceptedHot.Add(new GridCandidate(i, j, k, pt, true));
                            }
                        }
                        else
                        {
                            result.ColdConsidered++;
                            if (isInsideColdEnvelope(pt))
                            {
                                result.AcceptedCold.Add(new GridCandidate(i, j, k, pt, false));
                            }
                        }
                    }
                }
            }

            return result;
        }
    }

    public static class HotBorderClearanceCalculator
    {
        // Distance (mm) from the GTV border to the keep-out boundary for hot
        // spheres — the "must clear the border by this much" term. Does NOT
        // include the sphere radius: that is applied as a separate erosion
        // step in BuildHotRegion, so a sphere's radius isn't subtracted twice.
        // Manual Boost Mode uses a fixed user distance; otherwise it's derived
        // from the dose-gradient fall-off needed to drop from peak to peripheral dose.
        public static double ComputeMm(double peakDose, double periDose, double gradientPercentPerMm, bool manualBoost, double manualDistCm)
        {
            if (manualBoost)
            {
                return manualDistCm * 10.0;
            }

            double doseDrop = peakDose - periDose;
            double dropRatePerMm = peakDose * (gradientPercentPerMm / 100.0);
            return doseDrop / dropRatePerMm;
        }
    }
}
