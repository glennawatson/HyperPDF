// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// What a document keeps between renders: loaded fonts, decoded images, colour spaces, shadings, recorded pattern cells
/// and soft masks. Every cache is safe to use from many threads.
/// </summary>
[DebuggerDisplay("PdfRenderCache: {Images.Count} images, {Images.Bytes} bytes")]
internal sealed class PdfRenderCache
{
    /// <summary>Initializes a new instance of the <see cref="PdfRenderCache"/> class.</summary>
    /// <param name="document">The document.</param>
    internal PdfRenderCache(PdfDocument document)
        : this(document, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="PdfRenderCache"/> class whose content converts device colours through the output intent.</summary>
    /// <param name="document">The document.</param>
    /// <param name="deviceColors">The output intent substitutes for the device colour spaces, or <see langword="null"/> for none.</param>
    internal PdfRenderCache(PdfDocument document, OutputIntentColors? deviceColors)
    {
        Document = document;
        DeviceColors = deviceColors;
        LowerCa = document.Objects.Names.Intern("ca");
        GraphicsStateName = document.Objects.Names.Intern("GS");
        Thumb = document.Objects.Names.Intern("Thumb");
    }

    /// <summary>Gets the output intent substitutes for the device colour spaces, or <see langword="null"/> when content uses the fixed conversions.</summary>
    internal OutputIntentColors? DeviceColors { get; }

    /// <summary>Gets the <c>/GS</c> name generated appearances give their graphics state.</summary>
    internal PdfName GraphicsStateName { get; }

    /// <summary>Gets the <c>/Thumb</c> name of a page's thumbnail image.</summary>
    internal PdfName Thumb { get; }

    /// <summary>Gets or sets a value indicating whether pages recorded from now on simulate image overprint.</summary>
    internal bool SimulateOverprint
    {
        get => Volatile.Read(ref field);
        set => Volatile.Write(ref field, value);
    }

    /// <summary>Gets or sets the tint pages recorded from now on draw over fillable form fields.</summary>
    internal PdfFormHighlight FormHighlight
    {
        get => PdfFormHighlight.Unpack(FormHighlightBits);
        set => FormHighlightBits = value.Pack();
    }

    /// <summary>Gets the document.</summary>
    internal PdfDocument Document { get; }

    /// <summary>Gets the document's name table.</summary>
    internal PdfNameTable Names => Document.Objects.Names;

    /// <summary>Gets the <c>/ca</c> name, which the known-name list does not hold.</summary>
    internal PdfName LowerCa { get; }

    /// <summary>Gets the loaded fonts.</summary>
    internal PdfFontCache Fonts { get; } = new();

    /// <summary>Gets the decoded images, bounded by their pixel bytes.</summary>
    internal ImageCache Images { get; } = new();

    /// <summary>Gets the parsed colour space arrays.</summary>
    internal ObjectCache<PdfArray, PdfColorSpace> ColorSpaces { get; } = new();

    /// <summary>Gets the parsed shadings by dictionary.</summary>
    internal ObjectCache<PdfDictionary, PdfShading> Shadings { get; } = new();

    /// <summary>Gets the recorded tiling cells.</summary>
    internal ObjectCache<PatternKey, PatternCell> Cells { get; } = new();

    /// <summary>Gets the recorded soft masks.</summary>
    internal ObjectCache<SoftMaskKey, PdfSoftMask> SoftMasks { get; } = new();

    /// <summary>Gets or sets the form field tint, packed by <see cref="PdfFormHighlight.Pack"/>, so one atomic write publishes it.</summary>
    private long FormHighlightBits
    {
        get => Volatile.Read(ref field);
        set => Volatile.Write(ref field, value);
    }

    /// <summary>Creates a backend image from decoded PDF pixels.</summary>
    /// <param name="data">The decoded image.</param>
    /// <returns>The backend image, which the caller owns.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static IPdfRenderImage? ToRenderImage(PdfImageData data) => PdfDrawingServices.Backend.CreateImage(data);

    /// <summary>
    /// Empties every cache when the document is disposed. Images are released as their last user finishes, and later
    /// image requests fail with <see cref="ObjectDisposedException"/>; the other caches are emptied, so renders still
    /// running either finish with what they hold or fail when they read from the closed document.
    /// </summary>
    internal void Close()
    {
        Images.Close();
        Fonts.Clear();
        ColorSpaces.Clear();
        Shadings.Clear();
        Cells.Clear();
        SoftMasks.Clear();
    }

    /// <summary>Drops font-dependent entries after newly requested font data becomes available.</summary>
    internal void InvalidateFonts()
    {
        Fonts.Clear();
        Cells.Clear();
        SoftMasks.Clear();
    }

    /// <summary>Gets an image XObject ready to draw and marks it in use.</summary>
    /// <param name="stream">The image stream.</param>
    /// <returns>The image, which the caller releases; <see langword="null"/> when it cannot be decoded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ImageEntry? AcquireImage(PdfStream stream) =>
        AcquireImage(stream, 0);

    /// <summary>Gets a decoded image at the selected resolution.</summary>
    /// <param name="stream">The image stream.</param>
    /// <param name="reductionLevels">The finest levels omitted from its decode.</param>
    /// <returns>The image, which the caller releases; null when decoding fails.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ImageEntry? AcquireImage(PdfStream stream, int reductionLevels) =>
        Images.Acquire(new(stream, reductionLevels), DeviceColors, Decode);

    /// <summary>Gets a colour space from a value.</summary>
    /// <param name="value">A name or colour space array, resolved.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or null.</param>
    /// <returns>The colour space.</returns>
    internal PdfColorSpace GetColorSpace(PdfValue value, PdfDictionary? resources)
    {
        using var scope = OutputIntentColors.Enter(DeviceColors);
        return value.AsArray() is not { } array
            ? PdfColorSpace.Parse(value, resources)
            : ColorSpaces.GetOrCreate(array, resources, static (key, state) => PdfColorSpace.Parse(PdfValue.FromArray(key), state))
                ?? PdfColorSpace.DeviceGray;
    }

    /// <summary>Decodes an image stream.</summary>
    /// <param name="key">The stream and decoded resolution.</param>
    /// <param name="colors">The output intent substitutes for device colour spaces, or <see langword="null"/>.</param>
    /// <returns>The entry, or null.</returns>
    private static ImageEntry? Decode(ImageKey key, OutputIntentColors? colors)
    {
        using var scope = OutputIntentColors.Enter(colors);
        var data = PdfImageDecoder.DecodeCompact(key.Stream, key.ReductionLevels);
        if (data is null)
        {
            return null;
        }

        if (data.UnsupportedCodec != PdfImageCodec.None)
        {
            var image = PdfRenderHooks.UnsupportedImageDecoder?.Invoke(key.Stream);
            return image is null ? null : new ImageEntry(image, false, true);
        }

        var converted = ToRenderImage(data);
        return converted is null ? null : new ImageEntry(converted, data.IsStencilMask, data.Interpolate);
    }
}
