// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Functions;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Reusable font, image, colour, shading and mask data for document operations or renderers with equal image and drawing settings. Each cache
/// is safe to use from many threads.
/// </summary>
[DebuggerDisplay("PdfRenderCache: {Images.Count} images, {Images.Bytes} bytes")]
internal sealed class PdfRenderCache
{
    /// <summary>The pattern paints, made when the first pattern is selected.</summary>
    private ObjectCache<PatternPaintKey, PdfPatternPaint>? _patternPaints;

    /// <summary>The newest layer version recordings have run under.</summary>
    private int _layerVersion;

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
        Fonts = new(document.FontFactory);
        DeviceColors = deviceColors;
        LowerCa = document.Objects.Names.Intern("ca");
        UseBlackPointCompensation = document.Objects.Names.Intern("UseBlackPtComp");
        GraphicsStateName = document.Objects.Names.Intern("GS");
        Thumb = document.Objects.Names.Intern("Thumb");
        DefaultGray = document.Objects.Names.Intern("DefaultGray");
        DefaultRgb = document.Objects.Names.Intern("DefaultRGB");
        DefaultCmyk = document.Objects.Names.Intern("DefaultCMYK");
    }

    /// <summary>Gets the <c>/DefaultGray</c> name of a resource dictionary's colour space that replaces DeviceGray.</summary>
    internal PdfName DefaultGray { get; }

    /// <summary>Gets the <c>/DefaultRGB</c> name of a resource dictionary's colour space that replaces DeviceRGB.</summary>
    internal PdfName DefaultRgb { get; }

    /// <summary>Gets the <c>/DefaultCMYK</c> name of a resource dictionary's colour space that replaces DeviceCMYK.</summary>
    internal PdfName DefaultCmyk { get; }

    /// <summary>Gets or sets the colorants pages recorded from now on leave out; empty for none. Set before the cache is shared.</summary>
    internal ImmutableArray<string> HiddenColorants { get; set; } = [];

    /// <summary>Gets the transfer tables of the function values graphics states name.</summary>
    internal ObjectCache<PdfValue, PdfTransfer> Transfers { get; } = new();

    /// <summary>Gets the parsed black generation and undercolour removal functions.</summary>
    internal ObjectCache<PdfValue, PdfFunction> Functions { get; } = new();

    /// <summary>Gets the dash patterns of graphics state <c>/D</c> arrays.</summary>
    internal ObjectCache<PdfArray, DashPattern> Dashes { get; } = new();

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

    /// <summary>Gets the <c>/UseBlackPtComp</c> name of PDF 2.0 graphics states, which the known-name list does not hold.</summary>
    internal PdfName UseBlackPointCompensation { get; }

    /// <summary>Gets the loaded fonts.</summary>
    internal PdfFontCache Fonts { get; }

    /// <summary>Gets the decoded images, bounded by their pixel bytes.</summary>
    internal ImageCache Images { get; } = new();

    /// <summary>Gets the parsed colour space arrays.</summary>
    internal ObjectCache<ResourcedKey<PdfArray>, PdfColorSpace> ColorSpaces { get; } = new();

    /// <summary>Gets the parsed default colour space arrays.</summary>
    internal ObjectCache<ResourcedKey<PdfArray>, PdfColorSpace> DefaultSpaces { get; } = new();

    /// <summary>Gets the parsed shadings by dictionary and the colour space resources they were parsed with.</summary>
    internal ObjectCache<ResourcedKey<PdfDictionary>, PdfShading> Shadings { get; } = new();

    /// <summary>Gets the recorded tiling cells.</summary>
    internal ObjectCache<PatternKey, PatternCell> Cells { get; } = new();

    /// <summary>
    /// Gets the pattern paints made so far, so a pattern selected again at the same place reuses its paint. The cache is
    /// made when the first pattern is selected.
    /// </summary>
    internal ObjectCache<PatternPaintKey, PdfPatternPaint> PatternPaints
    {
        get
        {
            if (Volatile.Read(ref _patternPaints) is { } existing)
            {
                return existing;
            }

            var created = new ObjectCache<PatternPaintKey, PdfPatternPaint>();
            return Interlocked.CompareExchange(ref _patternPaints, created, null) ?? created;
        }
    }

    /// <summary>Gets the recorded soft masks.</summary>
    internal ObjectCache<SoftMaskKey, PdfSoftMask> SoftMasks { get; } = new();

    /// <summary>
    /// Gets the appearances generated for annotations that have none, valid for one revision of the document's objects. The
    /// cache is made when the first such annotation is drawn, so documents without them never create it.
    /// </summary>
    internal GeneratedAppearanceCache Appearances
    {
        get
        {
            if (Volatile.Read(ref field) is { } existing)
            {
                return existing;
            }

            var created = new GeneratedAppearanceCache();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

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
    internal IPdfRenderImage? ToRenderImage(PdfImageData data) => Document.DrawingServices.Backend.CreateImage(data);

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
        DefaultSpaces.Clear();
        Shadings.Clear();
        ClearRecordedPaints();
        Transfers.Clear();
        Functions.Clear();
        Dashes.Clear();
        Appearances.Clear();
    }

    /// <summary>Drops font-dependent entries after newly requested font data becomes available.</summary>
    internal void InvalidateFonts()
    {
        Fonts.Clear();
        ClearRecordedPaints();
        Appearances.Clear();
    }

    /// <summary>
    /// Notes the layer version a recording runs under. Cells and soft masks of an older version can no longer be asked for, so
    /// they are dropped when a newer version first appears instead of piling up with every layer switch.
    /// </summary>
    /// <param name="version">The layer version.</param>
    internal void ObserveLayerVersion(int version)
    {
        var seen = Volatile.Read(ref _layerVersion);
        while (version > seen)
        {
            var prior = Interlocked.CompareExchange(ref _layerVersion, version, seen);
            if (prior == seen)
            {
                ClearRecordedPaints();
                return;
            }

            seen = prior;
        }
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
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The image, which the caller releases; null when decoding fails.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ImageEntry? AcquireImage(PdfStream stream, int reductionLevels, CancellationToken cancellationToken = default) =>
        AcquireImage(stream, reductionLevels, IccRendering.Default, cancellationToken);

    /// <summary>Gets a decoded image at the selected resolution, its ICC colours converted with a rendering.</summary>
    /// <param name="stream">The image stream.</param>
    /// <param name="reductionLevels">The finest levels omitted from its decode.</param>
    /// <param name="rendering">The rendering intent and black point choice.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The image, which the caller releases; null when decoding fails.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ImageEntry? AcquireImage(PdfStream stream, int reductionLevels, IccRendering rendering, CancellationToken cancellationToken) =>
        AcquireImage(stream, reductionLevels, rendering, null, cancellationToken);

    /// <summary>Gets a decoded image at the selected resolution, its ICC colours converted with a rendering and its pixels mapped by a transfer function.</summary>
    /// <param name="stream">The image stream.</param>
    /// <param name="reductionLevels">The finest levels omitted from its decode.</param>
    /// <param name="rendering">The rendering intent and black point choice.</param>
    /// <param name="transfer">The transfer function, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The image, which the caller releases; null when decoding fails.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ImageEntry? AcquireImage(PdfStream stream, int reductionLevels, IccRendering rendering, PdfTransfer? transfer, CancellationToken cancellationToken) =>
        Images.Acquire(
            new(stream, reductionLevels, rendering, transfer?.Id ?? 0),
            new ImageDecodeState(this, DeviceColors, transfer, cancellationToken),
            Decode,
            cancellationToken);

    /// <summary>Gets a colour space from a value.</summary>
    /// <param name="value">A name or colour space array, resolved.</param>
    /// <param name="resources">The /ColorSpace resource dictionary, or null.</param>
    /// <returns>The colour space.</returns>
    internal PdfColorSpace GetColorSpace(PdfValue value, PdfDictionary? resources) =>
        value.AsArray() is not { } array
            ? PdfColorSpace.Parse(value, resources, DeviceColors)
            : ColorSpaces.GetOrCreate(new(array, resources), new ColorSpaceRequest(resources, DeviceColors), static (key, state) =>
                PdfColorSpace.Parse(PdfValue.FromArray(key.Source), state.Resources, state.Colors))
                ?? PdfColorSpace.DeviceGray;

    /// <summary>Gets the colour space a /DefaultGray, /DefaultRGB or /DefaultCMYK entry names, which keeps device spaces inside it as they are.</summary>
    /// <param name="array">The colour space array, resolved.</param>
    /// <param name="resources">The /ColorSpace resource dictionary.</param>
    /// <returns>The colour space, or <see langword="null"/> when it cannot be parsed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal PdfColorSpace? GetDefaultColorSpace(PdfArray array, PdfDictionary resources) =>
        DefaultSpaces.GetOrCreate(new(array, resources), new ColorSpaceRequest(resources, DeviceColors), static (key, state) =>
            ColorSpaceParser.ParseDefault(PdfValue.FromArray(key.Source), state.Resources, state.Colors));

    /// <summary>Decodes an image stream.</summary>
    /// <param name="key">The stream and decoded resolution.</param>
    /// <param name="state">The output intent colours and caller's cancellation token.</param>
    /// <returns>The entry, or null.</returns>
    private static ImageEntry? Decode(ImageKey key, ImageDecodeState state)
    {
        var data = PdfImageDecoder.DecodeCompact(key.Stream, key.ReductionLevels, state.Colors, key.Rendering, state.CancellationToken);
        if (data is null)
        {
            return null;
        }

        if (data.UnsupportedCodec != PdfImageCodec.None)
        {
            var image = state.Cache.Document.DrawingServices.UnsupportedImageDecoder?.Invoke(key.Stream, state.CancellationToken);
            return image is null ? null : new ImageEntry(image, false, true);
        }

        state.Transfer?.ApplyTo(data);
        var converted = state.Cache.ToRenderImage(data);
        return converted is null ? null : new ImageEntry(converted, data.IsStencilMask, data.Interpolate);
    }

    /// <summary>Empties the cells, soft masks and the pattern paints that hold the cells.</summary>
    private void ClearRecordedPaints()
    {
        Cells.Clear();
        SoftMasks.Clear();

        // The paints are made on first use, and clearing must not make them.
        Volatile.Read(ref _patternPaints)?.Clear();
    }

    /// <summary>The input to one image decode, passed to the cache leader without shared mutable state.</summary>
    /// <param name="Cache">The owning render cache.</param>
    /// <param name="Colors">The output intent colours.</param>
    /// <param name="Transfer">The transfer function applied to the decoded pixels, or null.</param>
    /// <param name="CancellationToken">The caller's token.</param>
    private readonly record struct ImageDecodeState(PdfRenderCache Cache, OutputIntentColors? Colors, PdfTransfer? Transfer, CancellationToken CancellationToken);

    /// <summary>The resource dictionary and output-intent colours for one space parse.</summary>
    /// <param name="Resources">The colour-space resources, or null.</param>
    /// <param name="Colors">The output-intent substitutions, or null.</param>
    private readonly record struct ColorSpaceRequest(PdfDictionary? Resources, OutputIntentColors? Colors);
}
