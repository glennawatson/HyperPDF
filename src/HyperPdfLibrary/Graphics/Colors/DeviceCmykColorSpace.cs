// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// The DeviceCMYK colour space; one shared instance. Converts to sRGB with a polynomial fitted to a
/// process-ink model (see <see cref="CmykConverter"/>).
/// </summary>
internal sealed class DeviceCmykColorSpace : PdfColorSpace
{
    /// <summary>The number of components.</summary>
    internal const int ComponentCount = 4;

    /// <summary>The index of the black component.</summary>
    private const int BlackIndex = 3;

    /// <summary>Initializes a new instance of the <see cref="DeviceCmykColorSpace"/> class.</summary>
    private DeviceCmykColorSpace()
    {
    }

    /// <inheritdoc/>
    public override int Components => ComponentCount;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => PdfColorSpaceKind.DeviceCmyk;

    /// <summary>Gets the shared instance.</summary>
    internal static DeviceCmykColorSpace Instance { get; } = new();

    /// <inheritdoc/>
    public override void GetInitialColor(Span<float> components)
    {
        components[..BlackIndex].Clear();
        components[BlackIndex] = 1;
    }

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb) =>
        CmykConverter.ToRgb(components[0], components[1], components[2], components[BlackIndex], rgb);

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount) =>
        CmykConverter.ToBgraRow(samples, bgra, pixelCount);
}
