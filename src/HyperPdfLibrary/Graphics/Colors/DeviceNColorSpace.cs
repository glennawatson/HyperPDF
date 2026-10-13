// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// A DeviceN colour space: n tints converted through a tint transform to an alternate space. NChannel attributes are
/// ignored. A one-tint space converts rows by table lookup; spaces of up to four tints convert through a cache of earlier
/// conversions; wider spaces convert each distinct pixel once per run.
/// </summary>
internal sealed class DeviceNColorSpace : PdfColorSpace
{
    /// <summary>The tint conversion.</summary>
    private readonly TintTransform _transform;

    /// <summary>The BGRA word for every 8-bit tint of a one-tint space, or <see langword="null"/>.</summary>
    private readonly uint[]? _lookup;

    /// <summary>The cache of earlier row conversions, or <see langword="null"/> for spaces too wide to key.</summary>
    private readonly ColorCache? _cache;

    /// <summary>The spaces made for each rendering that converts the alternate differently, or <see langword="null"/> before the first.</summary>
    private PdfColorSpace?[]? _renderings;

    /// <summary>Initializes a new instance of the <see cref="DeviceNColorSpace"/> class.</summary>
    /// <param name="components">The number of tints.</param>
    /// <param name="transform">The tint conversion.</param>
    /// <param name="isNone">Whether every colorant is /None.</param>
    /// <param name="colorants">The colorants' names, one per tint.</param>
    internal DeviceNColorSpace(int components, TintTransform transform, bool isNone, string[] colorants)
    {
        Components = components;
        ColorantNames = colorants;
        _transform = transform;
        PaintsNothing = isNone;
        _lookup = components == 1 ? BuildLookup(this) : null;
        _cache = components is > 1 and <= ColorCache.MaxComponents ? new() : null;
    }

    /// <inheritdoc/>
    public override int Components { get; }

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.DeviceN;

    /// <inheritdoc/>
    public override bool PaintsNothing { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<string> ColorantNames { get; }

    /// <summary>Gets the alternate colour space.</summary>
    internal PdfColorSpace Alternate => _transform.Alternate;

    /// <inheritdoc/>
    public override void GetInitialColor(Span<float> components) => components[..Components].Fill(1);

    /// <inheritdoc/>
    internal override PdfColorSpace ForRendering(IccRendering rendering)
    {
        var alternate = _transform.Alternate.ForRendering(rendering);
        return ReferenceEquals(alternate, _transform.Alternate)
            ? this
            : ColorSpaceRenderings.GetOrCreate(
                ref _renderings,
                rendering,
                this,
                alternate,
                static (owner, converting) => new DeviceNColorSpace(owner.Components, owner._transform with { Alternate = converting }, owner.PaintsNothing, [.. owner.ColorantNames]));
    }

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb) => _transform.ToRgb(components[..Components], rgb);

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount)
    {
        if (_lookup is not null)
        {
            PixelConverter.LookupToBgra(samples, bgra, pixelCount, _lookup);
            return;
        }

        ConvertRowScalar(samples, bgra, pixelCount, _cache);
    }
}
