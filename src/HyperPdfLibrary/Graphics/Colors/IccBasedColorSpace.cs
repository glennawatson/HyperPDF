// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// An ICCBased colour space. Matrix/TRC RGB and gray profiles and look-up-table profiles (CMYK, Lab, N-colour) are applied
/// in managed code; sRGB profiles use device RGB. Profiles that cannot be read convert through the /Alternate space, or the
/// device space with /N components. Device alternates keep their vectorised row conversion.
/// </summary>
internal sealed class IccBasedColorSpace : PdfColorSpace
{
    /// <summary>The number of colour components.</summary>
    private readonly int _components;

    /// <summary>The space colours convert through when there is no managed conversion, or <see langword="null"/> when there is always one.</summary>
    private readonly PdfColorSpace? _alternate;

    /// <summary>The component ranges as min/max pairs.</summary>
    private readonly float[] _range;

    /// <summary>The managed profile conversion, or <see langword="null"/> when colours convert through the alternate.</summary>
    private readonly IccTransform? _transform;

    /// <summary>Whether the range is [0 1] for every component, so samples pass straight to the alternate.</summary>
    private readonly bool _isUnitRange;

    /// <summary>The spaces made for each rendering that converts differently, or <see langword="null"/> before the first.</summary>
    private PdfColorSpace?[]? _renderings;

    /// <summary>Initializes a new instance of the <see cref="IccBasedColorSpace"/> class.</summary>
    /// <param name="alternate">The space colours convert through; it has the same number of components. <see langword="null"/> when <paramref name="transform"/> is set and no alternate fits.</param>
    /// <param name="range">The component ranges.</param>
    /// <param name="transform">The managed conversion of the profile, or <see langword="null"/> to use the alternate.</param>
    /// <param name="components">The number of components; the alternate's when it is set.</param>
    internal IccBasedColorSpace(PdfColorSpace? alternate, float[] range, IccTransform? transform, int components)
    {
        _alternate = alternate;
        _components = alternate?.Components ?? components;
        _range = range;
        _transform = transform;
        _isUnitRange = IsUnitRange(range);
    }

    /// <summary>Initializes a new instance of the <see cref="IccBasedColorSpace"/> class that converts through the alternate.</summary>
    /// <param name="alternate">The space colours convert through.</param>
    /// <param name="range">The component ranges.</param>
    /// <param name="transform">The managed conversion of the profile, or <see langword="null"/> to use the alternate.</param>
    internal IccBasedColorSpace(PdfColorSpace alternate, float[] range, IccTransform? transform)
        : this(alternate, range, transform, alternate.Components)
    {
    }

    /// <inheritdoc/>
    public override int Components => _components;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.IccBased;

    /// <summary>Gets the space colours convert through when the profile has no managed conversion, or <see langword="null"/>.</summary>
    internal PdfColorSpace? Alternate => _alternate;

    /// <inheritdoc/>
    public override void GetInitialColor(Span<float> components)
    {
        for (var i = 0; i < Components; i++)
        {
            components[i] = Math.Clamp(0, _range[PairSize * i], _range[(PairSize * i) + 1]);
        }
    }

    /// <inheritdoc/>
    public override float[] GetDefaultDecode(int bitsPerComponent) => (float[])_range.Clone();

    /// <inheritdoc/>
    internal override PdfColorSpace ForRendering(IccRendering rendering)
    {
        if (_transform is null)
        {
            return this;
        }

        var transform = _transform.ForRendering(rendering);
        return ReferenceEquals(transform, _transform)
            ? this
            : ColorSpaceRenderings.GetOrCreate(
                ref _renderings,
                rendering,
                this,
                transform,
                static (owner, converting) => new IccBasedColorSpace(owner._alternate, owner._range, converting, owner._components));
    }

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb)
    {
        if (_transform is null)
        {
            _alternate!.ToRgb(components, rgb);
            return;
        }

        _transform.ToRgb(components, rgb);
    }

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount)
    {
        if (_transform is not null && _isUnitRange)
        {
            _transform.ConvertRow(samples, bgra, pixelCount);
            return;
        }

        if (_isUnitRange && _alternate?.Kind is PdfColorSpaceKind.DeviceGray or PdfColorSpaceKind.DeviceRgb or PdfColorSpaceKind.DeviceCmyk)
        {
            _alternate.ConvertRow(samples, bgra, pixelCount);
            return;
        }

        ConvertRowScalar(samples, bgra, pixelCount);
    }

    /// <summary>Determines whether every range is [0 1].</summary>
    /// <param name="range">The ranges.</param>
    /// <returns><see langword="true"/> when all are [0 1].</returns>
    private static bool IsUnitRange(float[] range)
    {
        for (var i = 0; i < range.Length; i += PairSize)
        {
            if (range[i] is not 0 || range[i + 1] is not 1)
            {
                return false;
            }
        }

        return true;
    }
}
