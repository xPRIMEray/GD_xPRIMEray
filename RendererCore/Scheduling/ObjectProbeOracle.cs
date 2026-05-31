using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Godot;
using RendererCore.SceneSnapshot;
using SceneSnapshotModel = RendererCore.SceneSnapshot.SceneSnapshot;

namespace RendererCore.Scheduling
{
    // Temporary local definition until RendererCore.Data.TransportClass is implemented
    public enum TransportClass
    {
        Unknown = 0,
        Geometry = 1,
        Field = 2,
        Portal = 3,
        Boundary = 4
    }

    // === FUTURE LAYER DUMMIES (remove when RendererCore.Data is real) ===
    // Commented to avoid name collision with RendererCore.SceneSnapshot.SceneSnapshot
    // public sealed class CameraSnapshot { }
    // public sealed class SceneSnapshot { ... }

    public record ObjectDepthRecord
    {
        public int ObjectId { get; init; }
        public float MinDistance { get; init; }
        public float MaxDistance { get; init; }
        public Vector3 RepresentativeHit { get; init; }
        public int HitCount { get; init; }
        public TransportClass DominantClass { get; init; }
    }

    public record ObjectPriority
    {
        public int ObjectId { get; init; }
        public float CausalScore { get; init; }
        public int StableId { get; init; }
    }

    public class ObjectProbeOracle
    {
        private Dictionary<int, ObjectDepthRecord> _records = new();
        private List<ObjectPriority> _causalOrder = new();
        private ulong _lastProbeFrame = 0;

        public IReadOnlyDictionary<int, ObjectDepthRecord> Records => _records;
        public IReadOnlyList<ObjectPriority> CausalOrdering => _causalOrder;
        public double LastProbeMs { get; private set; } = 0;

        // =====================================================================
        // ORIGINAL FUTURE API (from STEP 1 spec) — preserved as comments until
        // RendererCore.Data, CameraSnapshot, and extended SceneSnapshot exist.
        // The active implementation for the current architecture is the Bridge below.
        // =====================================================================
        /*
        public void AcquireProbes(in CameraSnapshot cam, in SceneSnapshot scene, int samplesPerObject = 12)
        {
            _records.Clear();
            _causalOrder.Clear();

            var visibleObjects = scene.GetVisibleObjectIds(cam) ?? scene.GetAllObjectIds();

            foreach (var objId in visibleObjects)
            {
                var record = ProbeSingleObject(objId, cam, scene, samplesPerObject);
                if (record != null)
                    _records[objId] = record;
            }

            _causalOrder = _records.Values
                .Select(r => new ObjectPriority
                {
                    ObjectId = r.ObjectId,
                    CausalScore = r.MinDistance,
                    StableId = r.ObjectId
                })
                .OrderBy(p => p.CausalScore)
                .ThenBy(p => p.StableId)
                .ToList();

            _lastProbeFrame = scene.FrameId;
        }

        private ObjectDepthRecord? ProbeSingleObject(int objId, in CameraSnapshot cam, 
            in SceneSnapshot scene, int samples)
        {
            // Placeholder - replace with real coarse ray probe later
            return new ObjectDepthRecord
            {
                ObjectId = objId,
                MinDistance = 5.0f,
                MaxDistance = 50.0f,
                RepresentativeHit = new Vector3(0,0,-10),
                HitCount = 8,
                DominantClass = TransportClass.Geometry
            };
        }

        public bool NeedsReprobe(in SceneSnapshot scene) =>
            scene.ObjectsChanged || (scene.FrameId - _lastProbeFrame > 30);
        */

        // === BRIDGE FOR CURRENT ARCHITECTURE (until RendererCore.Data + CameraSnapshot exist) ===
        public void TryAcquireProbesBridge(SceneSnapshotModel snapshot, Camera3D camera, int samplesPerObject = 12)
        {
            var sw = Stopwatch.StartNew();
            _records.Clear();
            _causalOrder.Clear();

            if (snapshot == null || camera == null || !GodotObject.IsInstanceValid(camera))
            {
                LastProbeMs = 0;
                return;
            }

            // Collect candidate object centers from snapshot (real geometry / instances)
            var centers = new List<(int id, Vector3 center, Aabb3 bounds)>();
            int id = 0;

            if (snapshot.Instances != null && snapshot.Instances.Count > 0)
            {
                for (int i = 0; i < snapshot.Instances.Count; i++)
                {
                    var b = snapshot.Instances.WorldBounds[i];
                    if (b.Max.X > b.Min.X) // valid
                    {
                        var c = b.Center;
                        centers.Add((id++, new Vector3(c.X, c.Y, c.Z), b));
                    }
                }
            }
            else if (snapshot.Geometry != null && snapshot.Geometry.Count > 0)
            {
                for (int i = 0; i < snapshot.Geometry.Count; i++)
                {
                    var b = snapshot.Geometry.WorldBounds[i];
                    if (b.Max.X > b.Min.X)
                    {
                        var c = b.Center;
                        centers.Add((id++, new Vector3(c.X, c.Y, c.Z), b));
                    }
                }
            }

            if (centers.Count == 0)
            {
                // Fallback: synthetic distant objects so scheduler still gets some causal data
                for (int i = 0; i < 6; i++)
                {
                    float dist = 8.0f + i * 4.0f;
                    _records[i] = new ObjectDepthRecord
                    {
                        ObjectId = i,
                        MinDistance = dist,
                        MaxDistance = dist + 25.0f,
                        RepresentativeHit = new Vector3(0, 0, -dist),
                        HitCount = 3,
                        DominantClass = TransportClass.Geometry
                    };
                }
                _lastProbeFrame = (ulong)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 16);
                LastProbeMs = sw.Elapsed.TotalMilliseconds;
                RebuildCausalOrder();
                return;
            }

            // Real coarse probing using Godot physics (stable, reuses existing patterns in RayBeamRenderer)
            var space = camera.GetWorld3D()?.DirectSpaceState;
            Vector3 camPos = camera.GlobalPosition;

            int maxObjects = Math.Min(centers.Count, 48);
            for (int o = 0; o < maxObjects; o++)
            {
                var (objId, center, bounds) = centers[o];
                // center is already Godot.Vector3 here
                float minD = float.MaxValue;
                float maxD = 0f;
                Vector3 bestHit = center;
                int hits = 0;

                // Sample center + jittered points toward the AABB
                int samples = Math.Max(6, Math.Min(samplesPerObject, 16));
                for (int s = 0; s < samples; s++)
                {
                    Vector3 target = center;
                    if (s > 0)
                    {
                        // Jitter within extents
                        var ext = bounds.Extents * 0.6f;
                        target += new Vector3(
                            (float)(Random.Shared.NextDouble() - 0.5) * ext.X,
                            (float)(Random.Shared.NextDouble() - 0.5) * ext.Y,
                            (float)(Random.Shared.NextDouble() - 0.5) * ext.Z);
                    }

                    Vector3 dir = (target - camPos);
                    float len = dir.Length();
                    if (len < 0.01f) continue;
                    dir /= len;

                    bool hitSomething = false;
                    float dist = len;

                    if (space != null)
                    {
                        var query = PhysicsRayQueryParameters3D.Create(camPos, camPos + dir * (len + 2.0f));
                        query.CollideWithAreas = false;
                        query.CollideWithBodies = true;
                        var result = space.IntersectRay(query);
                        if (result.Count > 0)
                        {
                            Vector3 hitPos = (Vector3)result["position"];
                            dist = (hitPos - camPos).Length();
                            hitSomething = true;
                            bestHit = hitPos;
                        }
                    }

                    if (!hitSomething)
                    {
                        // Fallback: pure geometric distance to AABB center (conservative upper bound)
                        dist = len;
                    }

                    if (dist < minD) minD = dist;
                    if (dist > maxD) maxD = dist;
                    if (hitSomething) hits++;
                }

                if (minD > 9999f) { minD = 5f; maxD = 50f; }

                _records[objId] = new ObjectDepthRecord
                {
                    ObjectId = objId,
                    MinDistance = minD,
                    MaxDistance = Math.Max(maxD, minD + 1f),
                    RepresentativeHit = bestHit,
                    HitCount = Math.Max(1, hits),
                    DominantClass = TransportClass.Geometry
                };
            }

            RebuildCausalOrder();
            _lastProbeFrame = (ulong)(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 16);
            LastProbeMs = sw.Elapsed.TotalMilliseconds;
        }

        private void RebuildCausalOrder()
        {
            _causalOrder = _records.Values
                .Select(r => new ObjectPriority
                {
                    ObjectId = r.ObjectId,
                    CausalScore = r.MinDistance,
                    StableId = r.ObjectId
                })
                .OrderBy(p => p.CausalScore)
                .ThenBy(p => p.StableId)
                .ToList();
        }

        public IReadOnlyList<ObjectPriority> GetCurrentCausalOrder() => _causalOrder;
    }
}
