// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// A text device that records which character codes each font shows, so a font can be cut down to those glyphs. It
/// runs like text extraction: images are never decoded and every layer is read.
/// </summary>
[DebuggerDisplay("FontUsageDevice: {Codes.Count} fonts")]
internal sealed class FontUsageDevice : ITextObjectDevice, IDisposable
{
    /// <summary>The devices waiting for the next run, with the map and bit sets they grew.</summary>
    private readonly ReusableSlots<FontUsageDevice> _idle;

    /// <summary>The emptied bit sets of earlier runs, kept so a font that first shows a code takes one instead of making one.</summary>
    private readonly Stack<CodeBitSet> _spareSets = [];

    /// <summary>The font that showed the latest glyph; text mostly stays in one font, so most glyphs skip the map.</summary>
    private PdfFont? _lastFont;

    /// <summary>The codes of <see cref="_lastFont"/>.</summary>
    private CodeBitSet? _lastCodes;

    /// <summary>Whether the device has been given back, so a second dispose does not offer it twice.</summary>
    private int _returned;

    /// <summary>Initializes a new instance of the <see cref="FontUsageDevice"/> class.</summary>
    /// <param name="idle">The registered service's font-usage device pool.</param>
    private FontUsageDevice(ReusableSlots<FontUsageDevice> idle) => _idle = idle;

    /// <summary>Gets the codes each font shows, as pooled bit sets that the device disposes.</summary>
    internal Dictionary<PdfFont, CodeBitSet> Codes { get; } = [with(ReferenceEqualityComparer.Instance)];

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _returned, 1) != 0)
        {
            return;
        }

        // The last codes are one of the sets in the map, which is emptied below.
        _lastCodes?.Dispose();
        foreach (var codes in Codes.Values)
        {
            codes.Dispose();
            _spareSets.Push(codes);
        }

        Codes.Clear();
        _lastFont = null;
        _lastCodes = null;
        _ = _idle.Offer(this);
    }

    /// <inheritdoc/>
    public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
    {
        if (ReferenceEquals(glyph.Font, _lastFont))
        {
            _ = _lastCodes!.Add(glyph.Code);
            return;
        }

        ref var codes = ref CollectionsMarshal.GetValueRefOrAddDefault(Codes, glyph.Font, out _);
        codes ??= _spareSets.TryPop(out var spare) ? spare : new(true);
        _ = codes.Add(glyph.Code);
        _lastFont = glyph.Font;
        _lastCodes = codes;
    }

    /// <inheritdoc/>
    public void BeginTextObject()
    {
    }

    /// <inheritdoc/>
    public void Save()
    {
    }

    /// <inheritdoc/>
    public void Restore()
    {
    }

    /// <inheritdoc/>
    public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void Stroke(PdfPath path, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
    {
    }

    /// <inheritdoc/>
    public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void PaintShading(PdfShading shading, ref GraphicsState state)
    {
    }

    /// <inheritdoc/>
    public void BeginGroup(in GroupInfo group)
    {
    }

    /// <inheritdoc/>
    public void EndGroup(in GroupInfo group)
    {
    }

    /// <inheritdoc/>
    public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
    {
    }

    /// <inheritdoc/>
    public void EndMarkedContent()
    {
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IPictureDevice CreatePictureDevice(PdfRect cull) => new NullPictureDevice(cull);

    /// <summary>Takes an idle device, or makes one when none is idle.</summary>
    /// <param name="resources">The registered service's reusable state.</param>
    /// <returns>An empty device, which the caller disposes to hand it back.</returns>
    internal static FontUsageDevice Rent(OptimizerResources resources)
    {
        var idle = resources.FontDevices;
        var device = idle.Take() ?? new(idle);
        _ = Interlocked.Exchange(ref device._returned, 0);
        return device;
    }
}
