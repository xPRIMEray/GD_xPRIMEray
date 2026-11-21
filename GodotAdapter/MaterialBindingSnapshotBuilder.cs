using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Godot;
using XPrimeRay.ObserverInstrumentation.Materials;

namespace GodotAdapter;

/// <summary>
/// One binding row as resolved from the live scene: the library key and outcome, plus path-keyed evidence that does
/// not depend on runtime instance ids. <see cref="Detail"/> is adapter-level attribution that is finer than the frozen
/// library reason (for example <c>material_overlay</c>, which the library has no reason for).
/// </summary>
public readonly record struct MaterialBindingRecord(
    string BodyPath,
    string BodyType,
    int ShapeIndex,
    ulong ColliderInstanceId,
    string ShapeType,
    string MeshRef,
    string MaterialSource,
    bool IsBound,
    MaterialDescriptor Descriptor,
    MaterialUnsupportedReason Reason,
    string Detail);

/// <summary>An immutable snapshot: the library table plus its path-keyed manifest.</summary>
public sealed class MaterialBindingSnapshot
{
    internal MaterialBindingSnapshot(MaterialBindingTable table, MaterialBindingRecord[] records, string manifestText, string stateKey)
    {
        Table = table;
        Records = records;
        ManifestText = manifestText;
        ManifestSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(manifestText))).ToLowerInvariant();
        StateKey = stateKey;
    }

    public MaterialBindingTable Table { get; }
    public IReadOnlyList<MaterialBindingRecord> Records { get; }

    /// <summary>Canonical, path-keyed, instance-id-free evidence manifest (LF, UTF-8).</summary>
    public string ManifestText { get; }
    public string ManifestSha256 { get; }

    /// <summary>Manifest plus instance ids: equal state keys mean an identical table could be rebuilt.</summary>
    public string StateKey { get; }
}

/// <summary>
/// TEXTURE-R0A.2 main-thread adapter from Godot scene state to an immutable <see cref="MaterialBindingTable"/>.
/// Only this class reads Godot objects; the table it produces is plain managed data that workers may share.
/// <para>Inventory: every <see cref="CollisionObject3D"/> under the root, independent of collision layers, masks,
/// process mode and visibility, so deferred layer toggles can never change a binding. Key: collider instance id and
/// body shape index (<see cref="CollisionObject3D.ShapeOwnerGetShapeIndex"/>), the index Godot reports as a hit's
/// <c>shape</c>.</para>
/// <para>Presentation mesh: valid <c>xpr_presentation_mesh</c> metadata (a NodePath or string relative to the body
/// naming a <see cref="MeshInstance3D"/>), otherwise exactly one direct <see cref="MeshInstance3D"/> child.
/// Material: <c>material_override</c>, then the surface override, then the mesh surface material.</para>
/// </summary>
public static class MaterialBindingSnapshotBuilder
{
    public const string ManifestFormat = "xprimeray-material-binding-manifest-v1";
    public const string PresentationMeshMetaKey = "xpr_presentation_mesh";

    private static readonly StringName MetaKey = PresentationMeshMetaKey;
    private static readonly StringName ColorAParam = "color_a";
    private static readonly StringName ColorBParam = "color_b";
    private static readonly StringName ScaleParam = "checker_scale";

    /// <summary>Resolves every binding under <paramref name="root"/>, in canonical (path, shape) order.</summary>
    public static List<MaterialBindingRecord> Scan(Node root)
    {
        var records = new List<MaterialBindingRecord>();
        if (root == null || !GodotObject.IsInstanceValid(root)) return records;

        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            Node node = stack.Pop();
            if (node is CollisionObject3D body) ScanBody(root, body, records);
            int count = node.GetChildCount();
            for (int index = count - 1; index >= 0; index--) stack.Push(node.GetChild(index));
        }

        records.Sort(static (a, b) =>
        {
            int byPath = string.CompareOrdinal(a.BodyPath, b.BodyPath);
            return byPath != 0 ? byPath : a.ShapeIndex.CompareTo(b.ShapeIndex);
        });
        return records;
    }

    /// <summary>Change-detection key: the manifest plus the instance ids that key the table.</summary>
    public static string ComputeStateKey(List<MaterialBindingRecord> records, out string manifestText)
    {
        manifestText = BuildManifest(records);
        var builder = new StringBuilder(manifestText.Length + records.Count * 24);
        builder.Append(manifestText);
        foreach (MaterialBindingRecord record in records)
        {
            builder.Append(record.ColliderInstanceId.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }

        return builder.ToString();
    }

    public static MaterialBindingSnapshot Build(long generation, List<MaterialBindingRecord> records)
    {
        string stateKey = ComputeStateKey(records, out string manifestText);
        return Build(generation, records, manifestText, stateKey);
    }

    public static MaterialBindingSnapshot Build(long generation, List<MaterialBindingRecord> records, string manifestText, string stateKey)
    {
        var entries = new MaterialBindingEntry[records.Count];
        for (int index = 0; index < records.Count; index++)
        {
            MaterialBindingRecord record = records[index];
            var key = new MaterialBindingKey(record.ColliderInstanceId, record.ShapeIndex);
            entries[index] = record.IsBound
                ? MaterialBindingEntry.Bound(key, record.Descriptor)
                : MaterialBindingEntry.Unsupported(key, record.Reason);
        }

        MaterialBindingTable table = MaterialBindingTable.Build(generation, entries);
        return new MaterialBindingSnapshot(table, records.ToArray(), manifestText, stateKey);
    }

    private static void ScanBody(Node root, CollisionObject3D body, List<MaterialBindingRecord> records)
    {
        string bodyPath = root == body ? "." : root.GetPathTo(body).ToString();
        string bodyType = body.GetClass();
        ulong colliderId = body.GetInstanceId();

        MeshInstance3D mesh = ResolvePresentationMesh(body, out string meshRef, out string meshDetail, out MaterialUnsupportedReason meshReason);

        // Material resolution is per body: every shape of a body shares its presentation mesh.
        string materialSource = "none";
        bool bound = false;
        MaterialDescriptor flatOrChecker = default;
        MaterialUnsupportedReason materialReason = meshReason;
        string materialDetail = meshDetail;
        if (mesh != null)
        {
            ResolveMaterial(mesh, out materialSource, out bound, out flatOrChecker, out materialReason, out materialDetail);
            // Keep mesh-resolution evidence (for example invalid metadata that fell back to the child rule).
            materialDetail = meshDetail + materialDetail;
        }

        foreach (uint ownerId in body.GetShapeOwners())
        {
            Transform3D ownerTransform = body.ShapeOwnerGetTransform(ownerId);
            int shapeCount = body.ShapeOwnerGetShapeCount(ownerId);
            for (int local = 0; local < shapeCount; local++)
            {
                int shapeIndex = body.ShapeOwnerGetShapeIndex(ownerId, local);
                Shape3D shape = body.ShapeOwnerGetShape(ownerId, local);
                string shapeType = shape?.GetClass() ?? "null";

                bool isBound = bound;
                MaterialUnsupportedReason reason = materialReason;
                string detail = materialDetail;
                MaterialDescriptor descriptor = default;
                if (shape is ConcavePolygonShape3D || shape is HeightMapShape3D)
                {
                    // The film's hit id is face_index when Godot reports one, which only triangle-based shapes
                    // can; there it is not the shape index. Rejected conservatively for both shape classes.
                    isBound = false;
                    reason = MaterialUnsupportedReason.ConcaveFaceIndexAmbiguous;
                    detail = shape is ConcavePolygonShape3D ? "concave_shape" : "heightmap_shape";
                }
                else if (isBound)
                {
                    GeometryCorrespondence correspondence = ClassifyCorrespondence(shape, ownerTransform, mesh);
                    descriptor = flatOrChecker with { Correspondence = correspondence };
                    detail = materialDetail;
                }

                records.Add(new MaterialBindingRecord(
                    bodyPath, bodyType, shapeIndex, colliderId, shapeType, meshRef, materialSource,
                    isBound, descriptor, isBound ? MaterialUnsupportedReason.None : reason, detail));
            }
        }
    }

    private static MeshInstance3D ResolvePresentationMesh(
        CollisionObject3D body, out string meshRef, out string detail, out MaterialUnsupportedReason reason)
    {
        string metaDetail = "";
        if (body.HasMeta(MetaKey))
        {
            Variant value = body.GetMeta(MetaKey);
            NodePath path = value.VariantType switch
            {
                Variant.Type.NodePath => value.AsNodePath(),
                Variant.Type.String or Variant.Type.StringName => new NodePath(value.AsString()),
                _ => null,
            };
            if (path != null && !path.IsEmpty && body.GetNodeOrNull(path) is MeshInstance3D explicitMesh)
            {
                meshRef = "meta:" + path;
                detail = "";
                reason = MaterialUnsupportedReason.None;
                return explicitMesh;
            }

            metaDetail = "metadata_invalid;";
        }

        MeshInstance3D found = null;
        int foundIndex = -1;
        int meshChildren = 0;
        int childCount = body.GetChildCount();
        for (int index = 0; index < childCount; index++)
        {
            if (body.GetChild(index) is MeshInstance3D candidate)
            {
                meshChildren++;
                if (found == null)
                {
                    found = candidate;
                    foundIndex = index;
                }
            }
        }

        if (meshChildren == 1)
        {
            meshRef = "child:" + foundIndex.ToString(CultureInfo.InvariantCulture);
            detail = metaDetail;
            reason = MaterialUnsupportedReason.None;
            return found;
        }

        meshRef = "none";
        reason = meshChildren == 0 ? MaterialUnsupportedReason.NoPresentationMesh : MaterialUnsupportedReason.AmbiguousPresentationMesh;
        detail = metaDetail + (meshChildren == 0 ? "no_mesh_child" : "mesh_children=" + meshChildren.ToString(CultureInfo.InvariantCulture));
        return null;
    }

    private static void ResolveMaterial(
        MeshInstance3D meshInstance,
        out string source,
        out bool bound,
        out MaterialDescriptor descriptor,
        out MaterialUnsupportedReason reason,
        out string detail)
    {
        source = "none";
        bound = false;
        descriptor = default;
        Mesh mesh = meshInstance.Mesh;
        if (mesh == null)
        {
            reason = MaterialUnsupportedReason.NoPresentationMesh;
            detail = "mesh_resource_null";
            return;
        }

        if (mesh.GetSurfaceCount() != 1)
        {
            reason = MaterialUnsupportedReason.MultiSurfaceMesh;
            detail = "surfaces=" + mesh.GetSurfaceCount().ToString(CultureInfo.InvariantCulture);
            return;
        }

        if (meshInstance.MaterialOverlay != null)
        {
            // The frozen library has no overlay reason. ShaderNotRecognized is the table-level reason (an
            // unmodelled extra pass); the manifest keeps the exact cause. See the R0A.2 report.
            reason = MaterialUnsupportedReason.ShaderNotRecognized;
            detail = "material_overlay";
            return;
        }

        Material material;
        if (meshInstance.MaterialOverride != null)
        {
            material = meshInstance.MaterialOverride;
            source = "material_override";
        }
        else if (meshInstance.GetSurfaceOverrideMaterial(0) != null)
        {
            material = meshInstance.GetSurfaceOverrideMaterial(0);
            source = "surface_override";
        }
        else
        {
            material = mesh.SurfaceGetMaterial(0);
            source = material != null ? "mesh_surface" : "none";
        }

        if (material == null)
        {
            reason = MaterialUnsupportedReason.ShaderNotRecognized;
            detail = "no_material";
            return;
        }

        if (material.NextPass != null)
        {
            reason = MaterialUnsupportedReason.ShaderNotRecognized;
            detail = "next_pass";
            return;
        }

        switch (material)
        {
            case ShaderMaterial shaderMaterial:
                ResolveChecker(shaderMaterial, out bound, out descriptor, out reason, out detail);
                return;
            case BaseMaterial3D standard:
                ResolveFlat(standard, out bound, out descriptor, out reason, out detail);
                return;
            default:
                reason = MaterialUnsupportedReason.ShaderNotRecognized;
                detail = "material_class=" + material.GetClass();
                return;
        }
    }

    private static void ResolveChecker(
        ShaderMaterial material, out bool bound, out MaterialDescriptor descriptor, out MaterialUnsupportedReason reason, out string detail)
    {
        bound = false;
        descriptor = default;
        Shader shader = material.Shader;
        if (shader == null || shader.ResourcePath != WorldCheckerEvaluator.ShaderResourcePath)
        {
            reason = MaterialUnsupportedReason.ShaderNotRecognized;
            detail = "shader=" + (shader?.ResourcePath ?? "null");
            return;
        }

        byte[] code = Encoding.UTF8.GetBytes(shader.Code ?? string.Empty);
        string codeSha = Convert.ToHexString(SHA256.HashData(code)).ToLowerInvariant();
        if (codeSha != WorldCheckerEvaluator.ShaderSourceSha256)
        {
            reason = MaterialUnsupportedReason.ShaderSourceHashMismatch;
            detail = "shader_sha=" + codeSha;
            return;
        }

        WorldCheckerParams defaults = WorldCheckerEvaluator.ShaderDefaults;
        if (!TryColorParam(material, ColorAParam, defaults.ColorA, out Rgb colorA, out detail)
            || !TryColorParam(material, ColorBParam, defaults.ColorB, out Rgb colorB, out detail)
            || !TryScaleParam(material, defaults.Scale, out float scale, out detail))
        {
            reason = detail.StartsWith("nonfinite", StringComparison.Ordinal)
                ? MaterialUnsupportedReason.NonFiniteParameter
                : MaterialUnsupportedReason.ParameterMissingOrWrongType;
            return;
        }

        bound = true;
        reason = MaterialUnsupportedReason.None;
        detail = "";
        descriptor = MaterialDescriptor.CreateWorldChecker(new WorldCheckerParams(colorA, colorB, scale), GeometryCorrespondence.Proxy);
    }

    private static bool TryColorParam(ShaderMaterial material, StringName name, Rgb fallback, out Rgb value, out string detail)
    {
        Variant raw = material.GetShaderParameter(name);
        detail = "";
        switch (raw.VariantType)
        {
            case Variant.Type.Nil:
                value = fallback;
                return true;
            case Variant.Type.Vector3:
                Vector3 v = raw.AsVector3();
                value = new Rgb(v.X, v.Y, v.Z);
                break;
            case Variant.Type.Color:
                Color c = raw.AsColor();
                value = new Rgb(c.R, c.G, c.B);
                break;
            default:
                value = default;
                detail = "param_type:" + name + "=" + raw.VariantType;
                return false;
        }

        if (!value.IsFinite)
        {
            detail = "nonfinite:" + name;
            return false;
        }

        return true;
    }

    private static bool TryScaleParam(ShaderMaterial material, float fallback, out float value, out string detail)
    {
        Variant raw = material.GetShaderParameter(ScaleParam);
        detail = "";
        switch (raw.VariantType)
        {
            case Variant.Type.Nil:
                value = fallback;
                return true;
            case Variant.Type.Float:
            case Variant.Type.Int:
                value = (float)raw.AsDouble();
                break;
            default:
                value = 0f;
                detail = "param_type:checker_scale=" + raw.VariantType;
                return false;
        }

        if (!float.IsFinite(value))
        {
            detail = "nonfinite:checker_scale";
            return false;
        }

        if (!(value > 0f))
        {
            detail = "nonpositive:checker_scale";
            return false;
        }

        return true;
    }

    private static void ResolveFlat(
        BaseMaterial3D material, out bool bound, out MaterialDescriptor descriptor, out MaterialUnsupportedReason reason, out string detail)
    {
        bound = false;
        descriptor = default;
        detail = "";
        if (material.EmissionEnabled) reason = MaterialUnsupportedReason.EmissionEnabled;
        else if (material.AlbedoTexture != null || (material.DetailEnabled && material.DetailAlbedo != null)) reason = MaterialUnsupportedReason.TextureBound;
        else if (material.VertexColorUseAsAlbedo) reason = MaterialUnsupportedReason.VertexColorAlbedo;
        else if (material.Transparency != BaseMaterial3D.TransparencyEnum.Disabled || material.AlbedoColor.A < 1f) reason = MaterialUnsupportedReason.TransparencyEnabled;
        else
        {
            Color albedo = material.AlbedoColor;
            var color = new Rgb(albedo.R, albedo.G, albedo.B);
            if (!color.IsFinite)
            {
                reason = MaterialUnsupportedReason.NonFiniteParameter;
                detail = "nonfinite:albedo_color";
                return;
            }

            bound = true;
            reason = MaterialUnsupportedReason.None;
            descriptor = MaterialDescriptor.CreateFlat(color, GeometryCorrespondence.Proxy);
            return;
        }

        detail = "standard_material";
    }

    /// <summary>
    /// Exact only for a BoxShape3D against a direct-child BoxMesh of equal size with an equal local transform.
    /// Everything else, including a tessellated SphereMesh against an analytic SphereShape3D, is Proxy.
    /// </summary>
    private static GeometryCorrespondence ClassifyCorrespondence(Shape3D shape, Transform3D ownerTransform, MeshInstance3D meshInstance)
    {
        if (shape is BoxShape3D box
            && meshInstance.Mesh is BoxMesh boxMesh
            && meshInstance.GetParent() is CollisionObject3D
            && box.Size == boxMesh.Size
            && meshInstance.Transform == ownerTransform)
        {
            return GeometryCorrespondence.Exact;
        }

        return GeometryCorrespondence.Proxy;
    }

    private static string BuildManifest(List<MaterialBindingRecord> records)
    {
        var builder = new StringBuilder(256 + records.Count * 160);
        builder.Append(ManifestFormat).Append('\n');
        builder.Append("table_format=").Append(MaterialBindingTable.SerializationFormat).Append('\n');
        builder.Append("checker_shader_sha256=").Append(WorldCheckerEvaluator.ShaderSourceSha256).Append('\n');
        builder.Append("inward_bias_bits=").Append(Bits(MaterialSamplingConvention.InwardBiasMeters)).Append('\n');
        builder.Append("near_boundary_bits=").Append(Bits(MaterialSamplingConvention.NearBoundaryCells)).Append('\n');
        builder.Append("entries=").Append(records.Count.ToString(CultureInfo.InvariantCulture)).Append('\n');
        foreach (MaterialBindingRecord record in records)
        {
            builder.Append(record.BodyPath)
                .Append("\tshape=").Append(record.ShapeIndex.ToString(CultureInfo.InvariantCulture))
                .Append("\tbody=").Append(record.BodyType)
                .Append("\tshape_type=").Append(record.ShapeType)
                .Append("\tmesh=").Append(record.MeshRef)
                .Append("\tsource=").Append(record.MaterialSource);
            if (record.IsBound)
            {
                MaterialDescriptor d = record.Descriptor.Canonical();
                builder.Append("\tstatus=bound\tkind=").Append(d.Kind.ToString())
                    .Append("\tcorrespondence=").Append(d.Correspondence.ToString())
                    .Append("\tencoding=").Append(d.Encoding.ToString());
                if (d.Kind == MaterialKind.Flat)
                {
                    builder.Append("\tflat=").Append(RgbBits(d.Flat));
                }
                else
                {
                    builder.Append("\tcolor_a=").Append(RgbBits(d.Checker.ColorA))
                        .Append("\tcolor_b=").Append(RgbBits(d.Checker.ColorB))
                        .Append("\tscale=").Append(Bits(d.Checker.Scale));
                }
            }
            else
            {
                builder.Append("\tstatus=unsupported\treason=").Append(record.Reason.ToString());
            }

            if (!string.IsNullOrEmpty(record.Detail)) builder.Append("\tdetail=").Append(record.Detail);
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static string RgbBits(Rgb value) => Bits(value.R) + "," + Bits(value.G) + "," + Bits(value.B);

    private static string Bits(float value) =>
        BitConverter.SingleToUInt32Bits(value == 0f ? 0f : value).ToString("x8", CultureInfo.InvariantCulture);
}
