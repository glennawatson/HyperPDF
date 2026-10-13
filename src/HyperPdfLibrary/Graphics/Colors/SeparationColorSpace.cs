// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// A Separation colour space: one tint converted through a tint transform to an alternate space. The 256 possible 8-bit
/// tints are converted once into a BGRA table. /All converts like any colorant; /None paints nothing.
/// </summary>
internal sealed class SeparationColorSpace : PdfColorSpace
{
    /// <summary>The tint conversion.</summary>
    private readonly TintTransform _transform;

    /// <summary>The BGRA word for every 8-bit tint.</summary>
    private readonly uint[] _lookup;

    /// <summary>The spaces made for each rendering that converts the alternate differently, or <see langword="null"/> before the first.</summary>
    private PdfColorSpace?[]? _renderings;

    /// <summary>Initializes a new instance of the <see cref="SeparationColorSpace"/> class.</summary>
    /// <param name="transform">The tint conversion.</param>
    /// <param name="isNone">Whether the colorant is /None.</param>
    /// <param name="colorant">The colorant's name.</param>
    internal SeparationColorSpace(TintTransform transform, bool isNone, string colorant)
    {
        _transform = transform;
        PaintsNothing = isNone;
        ColorantNames = [colorant];
        _lookup = BuildLookup(this);
    }

    /// <inheritdoc/>
    public override int Components => 1;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.Separation;

    /// <inheritdoc/>
    public override bool PaintsNothing { get; }

    /// <inheritdoc/>
    public override IReadOnlyList<string> ColorantNames { get; }

    /// <summary>Gets the alternate colour space.</summary>
    internal PdfColorSpace Alternate => _transform.Alternate;

    /// <inheritdoc/>
    public override void GetInitialColor(Span<float> components) => components[0] = 1;

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
                static (owner, converting) => new SeparationColorSpace(owner._transform with { Alternate = converting }, owner.PaintsNothing, owner.ColorantNames[0]));
    }

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb) => _transform.ToRgb(components[..1], rgb);

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        PixelConverter.LookupToBgra(samples, bgra, pixelCount, _lookup);
}
