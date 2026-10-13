// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Shadings;

/// <summary>
/// A parsed shading dictionary. Gradient and function shadings build one immutable <see cref="IPdfRenderShader"/>; mesh shadings
/// build triangles. Both are made on first use and shared by every thread.
/// </summary>
[DebuggerDisplay("PdfShading: {Kind}")]
public sealed class PdfShading
{
    /// <summary>The smallest power-of-two scale a patch mesh is cut for.</summary>
    private const int MinScaleExponent = -6;

    /// <summary>The largest power-of-two scale a patch mesh is cut for.</summary>
    private const int MaxScaleExponent = 8;

    /// <summary>The ratio between neighbouring scale buckets.</summary>
    private const float ScaleBase = 2;

    /// <summary>The number of scale buckets.</summary>
    private const int ScaleBuckets = MaxScaleExponent - MinScaleExponent + 1;

    /// <summary>Guards building the backend resources.</summary>
    private readonly Lock _gate = new();

    /// <summary>Whether the shader has been built.</summary>
    private bool _shaderBuilt;

    /// <summary>The built shader, or null when the shading cannot be drawn with one.</summary>
    private IPdfRenderShader? _shader;

    /// <summary>Whether the mesh of each scale bucket has been built.</summary>
    private bool[]? _meshBuilt;

    /// <summary>The built meshes by scale bucket.</summary>
    private ShadingMesh?[]? _meshes;

    /// <summary>The shadings made for each rendering that converts the colour space differently, or <see langword="null"/> before the first.</summary>
    private PdfShading?[]? _renderings;

    /// <summary>The shadings made for each transfer function, or <see langword="null"/> before the first.</summary>
    private ConditionalWeakTable<PdfTransfer, PdfShading>? _transfers;

    /// <summary>Initializes a new instance of the <see cref="PdfShading"/> class.</summary>
    /// <param name="kind">The shading type.</param>
    /// <param name="dictionary">The shading dictionary.</param>
    /// <param name="stream">The shading stream for mesh types, otherwise null.</param>
    /// <param name="colorSpace">The colour space.</param>
    /// <param name="function">The colour function, or null.</param>
    private PdfShading(PdfShadingKind kind, PdfDictionary dictionary, PdfStream? stream, PdfColorSpace colorSpace, PdfFunction? function)
    {
        Kind = kind;
        Dictionary = dictionary;
        Stream = stream;
        ColorSpace = colorSpace;
        Function = function;
        BBox = dictionary.TryGetRectangle(KnownName.BBox, out var box) ? box : null;
        Background = ReadBackground(dictionary, colorSpace);
        AntiAlias = dictionary.GetBoolean(KnownName.AntiAlias);
    }

    /// <summary>Gets the shading type.</summary>
    public PdfShadingKind Kind { get; }

    /// <summary>Gets the shading dictionary.</summary>
    public PdfDictionary Dictionary { get; }

    /// <summary>Gets the shading stream for mesh types, otherwise null.</summary>
    public PdfStream? Stream { get; }

    /// <summary>Gets the colour space.</summary>
    public PdfColorSpace ColorSpace { get; }

    /// <summary>Gets the colour function, or null when colours are given directly (meshes only).</summary>
    public PdfFunction? Function { get; }

    /// <summary>Gets the bounding box in shading space, or null.</summary>
    public PdfRectangle? BBox { get; }

    /// <summary>Gets the background colour as 0xRRGGBB, or null.</summary>
    public uint? Background { get; }

    /// <summary>
    /// Gets a value indicating whether the shading dictionary asks for its function to be filtered against aliasing
    /// (ISO 32000-2 Table 77 /AntiAlias; default false). Devices may treat it as a quality hint.
    /// </summary>
    public bool AntiAlias { get; }

    /// <summary>Gets a value indicating whether the shading is drawn as triangles.</summary>
    public bool IsMesh => Kind >= PdfShadingKind.FreeForm;

    /// <summary>Parses a shading.</summary>
    /// <param name="value">The shading dictionary or stream, resolved.</param>
    /// <param name="colorSpaceResources">The /ColorSpace resource dictionary for named spaces, or null.</param>
    /// <returns>The shading, or null when it is damaged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfShading? Parse(PdfValue value, PdfDictionary? colorSpaceResources) => Parse(value, colorSpaceResources, null);

    /// <summary>Converts PDF colour components to a managed drawing colour.</summary>
    /// <param name="space">The colour space.</param>
    /// <param name="components">The components.</param>
    /// <returns>The opaque colour.</returns>
    public static PdfColor ToColor(PdfColorSpace space, ReadOnlySpan<float> components)
    {
        var colour = ColorState.Resolve(space, components);
        return new(colour.Red, colour.Green, colour.Blue);
    }

    /// <summary>Gets the shader for gradient and function shadings, building it on first use.</summary>
    /// <returns>The shader, or null when it cannot be built or the shading is a mesh.</returns>
    public IPdfRenderShader? GetShader()
    {
        lock (_gate)
        {
            if (!_shaderBuilt)
            {
                _shader = IsMesh ? null : ShadingShaders.Create(this);
                _shaderBuilt = true;
            }

            return _shader;
        }
    }

    /// <summary>
    /// Gets the triangles for mesh shadings, building them on first use. Patch meshes are cut more finely the larger
    /// they are drawn, so they are kept per power-of-two scale; triangle meshes do not depend on the scale.
    /// </summary>
    /// <param name="scale">The device units one unit of shading space spans when drawn.</param>
    /// <returns>The mesh, or null when it is empty or the shading is not a mesh.</returns>
    public ShadingMesh? GetMesh(float scale)
    {
        var bucket = Kind >= PdfShadingKind.Coons ? ScaleBucket(scale) : 0;
        lock (_gate)
        {
            _meshes ??= new ShadingMesh?[ScaleBuckets];
            _meshBuilt ??= new bool[ScaleBuckets];
            if (!_meshBuilt[bucket])
            {
                _meshes[bucket] = IsMesh ? MeshBuilder.Build(this, MathF.Pow(ScaleBase, bucket + MinScaleExponent)) : null;
                _meshBuilt[bucket] = true;
            }

            return _meshes[bucket];
        }
    }

    /// <summary>Parses a shading using output-intent colour substitutions.</summary>
    /// <param name="value">The shading dictionary or stream.</param>
    /// <param name="colorSpaceResources">The colour-space resources, or null.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <returns>The shading, or null when invalid.</returns>
    internal static PdfShading? Parse(PdfValue value, PdfDictionary? colorSpaceResources, OutputIntentColors? colors)
    {
        var dictionary = value.AsDictionary();
        if (dictionary is null)
        {
            return null;
        }

        var type = dictionary.GetInt32(KnownName.ShadingType);
        if (type is < (int)PdfShadingKind.FunctionBased or > (int)PdfShadingKind.Tensor)
        {
            return null;
        }

        var space = PdfColorSpace.Parse(dictionary.Get(KnownName.ColorSpace), colorSpaceResources, colors);
        var functionValue = dictionary.Get(KnownName.Function);
        var function = functionValue.IsNull ? null : PdfFunction.Parse(functionValue);
        var kind = (PdfShadingKind)type;
        var stream = value.AsStream();
        return kind >= PdfShadingKind.FreeForm && stream is null ? null : new(kind, dictionary, stream, space, function);
    }

    /// <summary>Gets the shading as it paints with a rendering intent and black point choice.</summary>
    /// <param name="rendering">The rendering in the graphics state at the <c>sh</c> or pattern selection.</param>
    /// <returns>This shading, or one whose colour space converts with the rendering; the same instance on every call.</returns>
    internal PdfShading ForRendering(IccRendering rendering)
    {
        var space = ColorSpace.ForRendering(rendering);
        return ReferenceEquals(space, ColorSpace)
            ? this
            : ColorSpaceRenderings.GetOrCreate(
                ref _renderings,
                rendering,
                this,
                space,
                static (owner, converting) => new PdfShading(owner.Kind, owner.Dictionary, owner.Stream, converting, owner.Function));
    }

    /// <summary>Gets the shading as it paints through a transfer function, which maps every colour it computes.</summary>
    /// <param name="transfer">The transfer function, or <see langword="null"/> for none.</param>
    /// <returns>This shading when there is no function, otherwise one whose colour space applies it; the same instance on every call.</returns>
    internal PdfShading ForTransfer(PdfTransfer? transfer)
    {
        if (transfer is null)
        {
            return this;
        }

        lock (_gate)
        {
            _transfers ??= new();
            if (!_transfers.TryGetValue(transfer, out var variant))
            {
                variant = new(Kind, Dictionary, Stream, new TransferColorSpace(ColorSpace, transfer), Function);
                _transfers.Add(transfer, variant);
            }

            return variant;
        }
    }

    /// <summary>Gets the bucket of a scale: the power of two at or above it, clamped to the kept range.</summary>
    /// <param name="scale">The scale.</param>
    /// <returns>The bucket index.</returns>
    private static int ScaleBucket(float scale)
    {
        var exponent = scale > 0 && float.IsFinite(scale) ? (int)MathF.Ceiling(MathF.Log2(scale)) : 0;
        return Math.Clamp(exponent, MinScaleExponent, MaxScaleExponent) - MinScaleExponent;
    }

    /// <summary>Reads the /Background colour.</summary>
    /// <param name="dictionary">The shading dictionary.</param>
    /// <param name="space">The colour space.</param>
    /// <returns>The colour, or null.</returns>
    private static uint? ReadBackground(PdfDictionary dictionary, PdfColorSpace space)
    {
        var array = dictionary.GetArray(KnownName.Background);
        if (array is null || array.Count < space.Components || space.Components > PdfColorSpace.MaxComponents)
        {
            return null;
        }

        Span<float> components = stackalloc float[PdfColorSpace.MaxComponents];
        _ = array.ReadNumbers(components);
        return ColorState.Resolve(space, components).Rgb;
    }
}
