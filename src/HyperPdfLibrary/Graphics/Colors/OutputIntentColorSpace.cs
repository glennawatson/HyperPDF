// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// A device colour space whose colours convert through the output intent profile. It keeps the device space's initial
/// colour (black for DeviceCMYK), which an ICCBased space would not.
/// </summary>
[DebuggerDisplay("OutputIntentColorSpace: {_device}")]
internal sealed class OutputIntentColorSpace : PdfColorSpace
{
    /// <summary>The device space the content named.</summary>
    private readonly PdfColorSpace _device;

    /// <summary>The profile space colours convert through.</summary>
    private readonly PdfColorSpace _profile;

    /// <summary>The spaces made for each rendering that converts the profile differently, or <see langword="null"/> before the first.</summary>
    private PdfColorSpace?[]? _renderings;

    /// <summary>Initializes a new instance of the <see cref="OutputIntentColorSpace"/> class.</summary>
    /// <param name="device">The device space the content named.</param>
    /// <param name="profile">The profile space colours convert through; it has the same component count.</param>
    internal OutputIntentColorSpace(PdfColorSpace device, PdfColorSpace profile)
    {
        _device = device;
        _profile = profile;
    }

    /// <inheritdoc/>
    public override int Components => _device.Components;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.IccBased;

    /// <inheritdoc/>
    internal override PdfColorSpaceKind NamedKind => _device.Kind;

    /// <inheritdoc/>
    public override void GetInitialColor(Span<float> components) => _device.GetInitialColor(components);

    /// <inheritdoc/>
    internal override PdfColorSpace ForRendering(IccRendering rendering)
    {
        var profile = _profile.ForRendering(rendering);
        return ReferenceEquals(profile, _profile)
            ? this
            : ColorSpaceRenderings.GetOrCreate(
                ref _renderings,
                rendering,
                this,
                profile,
                static (owner, converting) => new OutputIntentColorSpace(owner._device, converting));
    }

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb) => _profile.ToRgb(components, rgb);

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        _profile.ConvertRow(samples, bgra, pixelCount);
}
