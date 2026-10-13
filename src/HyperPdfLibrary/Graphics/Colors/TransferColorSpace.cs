// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>A colour space whose RGB results go through a transfer function, so shadings drawn in it show the function's effect.</summary>
[DebuggerDisplay("TransferColorSpace: {_inner}")]
internal sealed class TransferColorSpace : PdfColorSpace
{
    /// <summary>The largest channel value as a float.</summary>
    private const float ChannelMax = 255;

    /// <summary>The space that converts the components.</summary>
    private readonly PdfColorSpace _inner;

    /// <summary>The transfer function applied to its RGB.</summary>
    private readonly PdfTransfer _transfer;

    /// <summary>Initializes a new instance of the <see cref="TransferColorSpace"/> class.</summary>
    /// <param name="inner">The space that converts the components.</param>
    /// <param name="transfer">The transfer function applied to its RGB.</param>
    internal TransferColorSpace(PdfColorSpace inner, PdfTransfer transfer)
    {
        _inner = inner;
        _transfer = transfer;
    }

    /// <inheritdoc/>
    public override int Components => _inner.Components;

    /// <inheritdoc/>
    public override PdfColorSpaceKind Kind => _inner.Kind;

    /// <inheritdoc/>
    public override bool PaintsNothing => _inner.PaintsNothing;

    /// <inheritdoc/>
    public override IReadOnlyList<string>? ColorantNames => _inner.ColorantNames;

    /// <inheritdoc/>
    public override void GetInitialColor(Span<float> components) => _inner.GetInitialColor(components);

    /// <inheritdoc/>
    public override float[] GetDefaultDecode(int bitsPerComponent) => _inner.GetDefaultDecode(bitsPerComponent);

    /// <inheritdoc/>
    internal override PdfColorSpace ForRendering(IccRendering rendering)
    {
        var converting = _inner.ForRendering(rendering);
        return ReferenceEquals(converting, _inner) ? this : new TransferColorSpace(converting, _transfer);
    }

    /// <inheritdoc/>
    private protected override void ToRgbCore(ReadOnlySpan<float> components, Span<float> rgb)
    {
        _inner.ToRgb(components, rgb);
        rgb[0] = Map(_transfer.Red, rgb[0]);
        rgb[1] = Map(_transfer.Green, rgb[1]);
        rgb[RgbComponents - 1] = Map(_transfer.Blue, rgb[RgbComponents - 1]);
    }

    /// <inheritdoc/>
    private protected override void ConvertRowCore(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount)
    {
        _inner.ConvertRow(samples, bgra, pixelCount);
        _transfer.ApplyBgra(bgra[..(pixelCount * PixelConverter.BytesPerPixel)]);
    }

    /// <summary>Maps one channel through a table.</summary>
    /// <param name="table">The table.</param>
    /// <param name="value">The channel from 0 to 1.</param>
    /// <returns>The mapped channel from 0 to 1.</returns>
    private static float Map(ReadOnlySpan<byte> table, float value) =>
        table[(int)MathF.Round(Math.Clamp(value, 0, 1) * ChannelMax)] / ChannelMax;
}
