using System.Numerics;

namespace XPrimeRay.Spatial;

public readonly struct SurfaceHit
{
    public string CanonicalPrimitiveId { get; }
    public SpatialSurfaceClass SurfaceClass { get; }
    public float SegmentT { get; }
    public Vector3 Position { get; }
    public Vector3 Normal { get; }
    public bool NormalValid { get; }

    public SurfaceHit(string canonicalPrimitiveId, SpatialSurfaceClass surfaceClass, float segmentT, Vector3 position, Vector3 normal, bool normalValid)
    {
        CanonicalPrimitiveId = canonicalPrimitiveId;
        SurfaceClass = surfaceClass;
        SegmentT = segmentT;
        Position = position;
        Normal = normal;
        NormalValid = normalValid;
    }
}

public interface IPass1SpatialQuery
{
    string AuthorityToken { get; }
    bool IntersectsSegment(Vector3 from, Vector3 to, uint collisionMask, out SurfaceHit hit);
}

public sealed class LinearScanSpatialQuery : IPass1SpatialQuery
{
    public const string AuthorityTokenValue = "XPrimeRaySpatialKernel/LinearScan-v0";
    public const string IntersectionPolicyVersion = "finite-segment-oriented-box-slab-v0";
    public const string MixedIntersectionPolicyVersion = "finite-segment-box-slab-v0+sphere-quadratic-v0";
    private readonly FrozenGeometrySnapshot _snapshot;

    public string AuthorityToken => AuthorityTokenValue;
    public long QueryCount { get; private set; }
    public long PrimitiveTestCount { get; private set; }

    public LinearScanSpatialQuery(FrozenGeometrySnapshot snapshot)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
    }

    public bool IntersectsSegment(Vector3 from, Vector3 to, uint collisionMask, out SurfaceHit hit)
    {
        QueryCount++;
        bool found = false;
        SurfaceHit nearest = default;
        foreach (IFrozenSpatialPrimitive primitive in _snapshot.AllPrimitives)
        {
            if (primitive.IsExcluded || (primitive.CollisionLayer & collisionMask) == 0)
                continue;
            PrimitiveTestCount++;
            if (!FrozenPrimitiveIntersection.TryIntersectSegment(primitive, from, to, out SurfaceHit candidate))
                continue;
            if (!found || candidate.SegmentT < nearest.SegmentT ||
                (candidate.SegmentT == nearest.SegmentT && string.CompareOrdinal(candidate.CanonicalPrimitiveId, nearest.CanonicalPrimitiveId) < 0))
            {
                found = true;
                nearest = candidate;
            }
        }
        hit = nearest;
        return found;
    }
}

public static class FrozenPrimitiveIntersection
{
    public static bool TryIntersectSegment(IFrozenSpatialPrimitive primitive, Vector3 from, Vector3 to, out SurfaceHit hit) => primitive switch
    {
        FrozenOrientedBox box => OrientedBoxIntersection.TryIntersectSegment(box, from, to, out hit),
        FrozenSphere sphere => SphereIntersection.TryIntersectSegment(sphere, from, to, out hit),
        _ => throw new NotSupportedException($"Unsupported frozen primitive kind: {primitive.GetType().FullName}")
    };
}

public static class SphereIntersection
{
    /// <summary>
    /// finite-segment-sphere-quadratic-v0: inclusive [0,1], no added
    /// tolerance, tangent/endpoint contacts included, and start-inside or
    /// start-on-surface returns t=0 with no outward normal.
    /// </summary>
    public static bool TryIntersectSegment(FrozenSphere sphere, Vector3 from, Vector3 to, out SurfaceHit hit)
    {
        Vector3 offset = from - sphere.Center;
        double radiusSquared = (double)sphere.Radius * sphere.Radius;
        if (Vector3.Dot(offset, offset) <= radiusSquared)
        {
            hit = new SurfaceHit(sphere.CanonicalPrimitiveId, sphere.SurfaceClass, 0f, from, Vector3.Zero, false);
            return true;
        }

        Vector3 direction = to - from;
        double a = Vector3.Dot(direction, direction);
        if (a == 0.0)
        {
            hit = default;
            return false;
        }
        double b = 2.0 * Vector3.Dot(offset, direction);
        double c = Vector3.Dot(offset, offset) - radiusSquared;
        double discriminant = b * b - 4.0 * a * c;
        if (discriminant < 0.0)
        {
            hit = default;
            return false;
        }

        double root = Math.Sqrt(discriminant);
        double t0 = (-b - root) / (2.0 * a);
        double t1 = (-b + root) / (2.0 * a);
        double t = t0 >= 0.0 && t0 <= 1.0 ? t0 : t1;
        if (t < 0.0 || t > 1.0)
        {
            hit = default;
            return false;
        }

        float segmentT = (float)t;
        Vector3 position = from + direction * segmentT;
        Vector3 normal = position - sphere.Center;
        bool normalValid = normal.LengthSquared() > 1e-20f;
        if (normalValid) normal = Vector3.Normalize(normal);
        hit = new SurfaceHit(sphere.CanonicalPrimitiveId, sphere.SurfaceClass, segmentT, position, normal, normalValid);
        return true;
    }
}

public static class OrientedBoxIntersection
{
    /// <summary>
    /// finite-segment-oriented-box-slab-v0:
    /// finite inclusive [0,1] segment; exact floating-point arithmetic; no
    /// added epsilon; grazing/tangent and endpoint touches are hits; parallel
    /// slabs use an exact-zero direction test; a start-inside segment returns
    /// t=0; equal-t nearest results are resolved by canonical primitive ID.
    /// </summary>
    public static bool TryIntersectSegment(FrozenOrientedBox box, Vector3 from, Vector3 to, out SurfaceHit hit)
    {
        Vector3 localFrom = Vector3.Transform(from, box.LocalFromWorld);
        Vector3 localTo = Vector3.Transform(to, box.LocalFromWorld);
        Vector3 direction = localTo - localFrom;
        Vector3 h = box.HalfExtents;

        if (IsInside(localFrom, h))
        {
            hit = new SurfaceHit(box.CanonicalPrimitiveId, box.SurfaceClass, 0f, from, Vector3.Zero, false);
            return true;
        }

        if (direction == Vector3.Zero)
        {
            hit = default;
            return false;
        }

        double enter = 0.0;
        double exit = 1.0;
        Vector3 enterNormal = Vector3.Zero;
        for (int axis = 0; axis < 3; axis++)
        {
            double origin = Get(localFrom, axis);
            double delta = Get(direction, axis);
            double extent = Get(h, axis);
            if (delta == 0.0)
            {
                if (origin < -extent || origin > extent)
                {
                    hit = default;
                    return false;
                }
                continue;
            }

            double a = (-extent - origin) / delta;
            double b = (extent - origin) / delta;
            Vector3 nearNormal;
            if (a <= b)
            {
                nearNormal = Axis(axis, -1f);
            }
            else
            {
                (a, b) = (b, a);
                nearNormal = Axis(axis, 1f);
            }

            if (a > enter)
            {
                enter = a;
                enterNormal = nearNormal;
            }
            if (b < exit) exit = b;
            if (enter > exit) break;
        }

        if (enter > exit || exit < 0.0 || enter > 1.0)
        {
            hit = default;
            return false;
        }

        float t = (float)Math.Clamp(enter, 0.0, 1.0);
        Vector3 localPosition = localFrom + direction * t;
        Vector3 worldPosition = Vector3.Transform(localPosition, box.WorldFromLocal);
        Vector3 worldNormal = Vector3.TransformNormal(enterNormal, box.WorldFromLocal);
        bool normalValid = worldNormal.LengthSquared() > 1e-20f;
        if (normalValid) worldNormal = Vector3.Normalize(worldNormal);
        hit = new SurfaceHit(box.CanonicalPrimitiveId, box.SurfaceClass, t, worldPosition, worldNormal, normalValid);
        return true;
    }

    private static bool IsInside(Vector3 p, Vector3 h) =>
        p.X >= -h.X && p.X <= h.X && p.Y >= -h.Y && p.Y <= h.Y && p.Z >= -h.Z && p.Z <= h.Z;

    private static double Get(Vector3 value, int axis) => axis switch { 1 => value.Y, 2 => value.Z, _ => value.X };

    private static Vector3 Axis(int axis, float sign) => axis switch
    {
        1 => new Vector3(0f, sign, 0f),
        2 => new Vector3(0f, 0f, sign),
        _ => new Vector3(sign, 0f, 0f)
    };
}
