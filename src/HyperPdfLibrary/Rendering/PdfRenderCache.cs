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
[DebuggerDisplay("PdfRenderCache: {ImageCapacity} bytes of images kept")]
internal sealed class PdfRenderCache
{
    /// <summary>The pattern paints, made when the first pattern is selected.</summary>
    private ObjectCache<PatternPaintKey, PdfPatternPaint>? _patternPaints;

    /// <summary>The decoded images, made when the first image is asked for.</summary>
    private ImageCache? _images;

    /// <summary>One once <see cref="Close"/> has run, so an image cache made afterwards starts closed.</summary>
    private int _closed;

    /// <summary>The loaded fonts, made when the first font is asked for.</summary>
    private PdfFontCache? _fonts;

    /// <summary>The transfer tables, made when the first graphics state names a transfer function.</summary>
    private ObjectCache<PdfValue, PdfTransfer>? _transfers;

    /// <summary>The black generation and undercolour removal functions, made when the first is read.</summary>
    private ObjectCache<PdfValue, PdfFunction>? _functions;

    /// <summary>The dash patterns, made when the first dash array is read.</summary>
    private ObjectCache<PdfArray, DashPattern>? _dashes;

    /// <summary>The parsed colour space arrays, made when the first array is read.</summary>
    private ObjectCache<ResourcedKey<PdfArray>, PdfColorSpace>? _colorSpaces;

    /// <summary>The parsed default colour space arrays, made when the first is read.</summary>
    private ObjectCache<ResourcedKey<PdfArray>, PdfColorSpace>? _defaultSpaces;

    /// <summary>The parsed shadings, made when the first is read.</summary>
    private ObjectCache<ResourcedKey<PdfDictionary>, PdfShading>? _shadings;

    /// <summary>The recorded tiling cells, made when the first is recorded.</summary>
    private ObjectCache<PatternKey, PatternCell>? _cells;

    /// <summary>The recorded soft masks, made when the first is recorded.</summary>
    private ObjectCache<SoftMaskKey, PdfSoftMask>? _softMasks;

    /// <summary>The generated annotation appearances, made when the first annotation without one is drawn.</summary>
    private GeneratedAppearanceCache? _appearances;

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
    internal ObjectCache<PdfValue, PdfTransfer> Transfers => LazyCaches.Get(ref _transfers);

    /// <summary>Gets the parsed black generation and undercolour removal functions.</summary>
    internal ObjectCache<PdfValue, PdfFunction> Functions => LazyCaches.Get(ref _functions);

    /// <summary>Gets the dash patterns of graphics state <c>/D</c> arrays.</summary>
    internal ObjectCache<PdfArray, DashPattern> Dashes => LazyCaches.Get(ref _dashes);

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

    /// <summary>Gets or sets how pages recorded from now on draw edges, thin lines and scaled images. Set before the cache is shared.</summary>
    internal PdfRenderQuality Quality { get; set; } = PdfRenderQuality.Default;

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
    internal PdfFontCache Fonts
    {
        get
        {
            if (Volatile.Read(ref _fonts) is { } existing)
            {
                return existing;
            }

            var created = new PdfFontCache(Document.FontFactory);
            return Interlocked.CompareExchange(ref _fonts, created, null) ?? created;
        }
    }

    /// <summary>Gets or sets the most pixel bytes of decoded images kept. Set before the cache is shared.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is negative.</exception>
    internal long ImageCapacity
    {
        get => Volatile.Read(ref field);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            Volatile.Write(ref field, value);
            if (Volatile.Read(ref _images) is { } existing)
            {
                existing.Capacity = value;
            }
        }
    } = ImageCache.DefaultCapacity;

    /// <summary>Gets the decoded images, bounded by their pixel bytes. The cache is made when the first image is asked for, so documents without images never make it.</summary>
    internal ImageCache Images
    {
        get
        {
            if (Volatile.Read(ref _images) is { } existing)
            {
                return existing;
            }

            var created = new ImageCache { Capacity = ImageCapacity };
            var published = Interlocked.CompareExchange(ref _images, created, null) ?? created;

            // A close that ran before the cache existed must still refuse images; the check follows the publish so one of the two sees the other.
            if (Volatile.Read(ref _closed) != 0)
            {
                published.Close();
            }

            return published;
        }
    }

    /// <summary>Gets the parsed colour space arrays.</summary>
    internal ObjectCache<ResourcedKey<PdfArray>, PdfColorSpace> ColorSpaces => LazyCaches.Get(ref _colorSpaces);

    /// <summary>Gets the parsed default colour space arrays.</summary>
    internal ObjectCache<ResourcedKey<PdfArray>, PdfColorSpace> DefaultSpaces => LazyCaches.Get(ref _defaultSpaces);

    /// <summary>Gets the parsed shadings by dictionary and the colour space resources they were parsed with.</summary>
    internal ObjectCache<ResourcedKey<PdfDictionary>, PdfShading> Shadings => LazyCaches.Get(ref _shadings);

    /// <summary>Gets the recorded tiling cells.</summary>
    internal ObjectCache<PatternKey, PatternCell> Cells => LazyCaches.Get(ref _cells);

    /// <summary>
    /// Gets the pattern paints made so far, so a pattern selected again at the same place reuses its paint. The cache is
    /// made when the first pattern is selected.
    /// </summary>
    internal ObjectCache<PatternPaintKey, PdfPatternPaint> PatternPaints => LazyCaches.Get(ref _patternPaints);

    /// <summary>Gets the recorded soft masks.</summary>
    internal ObjectCache<SoftMaskKey, PdfSoftMask> SoftMasks => LazyCaches.Get(ref _softMasks);

    /// <summary>
    /// Gets the appearances generated for annotations that have none, valid for one revision of the document's objects. The
    /// cache is made when the first such annotation is drawn, so documents without them never create it.
    /// </summary>
    internal GeneratedAppearanceCache Appearances
    {
        get
        {
            if (Volatile.Read(ref _appearances) is { } existing)
            {
                return existing;
            }

            var created = new GeneratedAppearanceCache();
            return Interlocked.CompareExchange(ref _appearances, created, null) ?? created;
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
        Volatile.Write(ref _closed, 1);
        Volatile.Read(ref _images)?.Close();
        Volatile.Read(ref _fonts)?.Clear();
        LazyCaches.Clear(ref _colorSpaces);
        LazyCaches.Clear(ref _defaultSpaces);
        LazyCaches.Clear(ref _shadings);
        ClearRecordedPaints();
        LazyCaches.Clear(ref _transfers);
        LazyCaches.Clear(ref _functions);
        LazyCaches.Clear(ref _dashes);
        Volatile.Read(ref _appearances)?.Clear();
    }

    /// <summary>Drops font-dependent entries after newly requested font data becomes available.</summary>
    internal void InvalidateFonts()
    {
        Volatile.Read(ref _fonts)?.Clear();
        ClearRecordedPaints();
        Volatile.Read(ref _appearances)?.Clear();
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
        var data = DecodeAtDisplaySize(key, state);
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

    /// <summary>Decodes an image stream at its display resolution.</summary>
    /// <param name="key">The stream and decoded resolution.</param>
    /// <param name="state">The output intent colours and caller's cancellation token.</param>
    /// <returns>The pixels, or null.</returns>
    private static PdfImageData? DecodeAtDisplaySize(ImageKey key, in ImageDecodeState state)
    {
        var nativeLevels = ImageReduction.NativeLevels(key.Stream.Dictionary, key.ReductionLevels);

        // A codec that cannot decode a lower resolution returns every pixel; raw samples are averaged down to the display
        // size while their rows are converted, and the pixels of any other codec after decoding.
        ImageLevels levels = new(nativeLevels, nativeLevels == 0 ? key.ReductionLevels : 0);
        ImageColors colors = new(state.Colors, key.Rendering);
        var data = PdfImageDecoder.DecodeCompact(key.Stream, levels, colors, state.CancellationToken);
        return data is { IsBoxReduced: false, UnsupportedCodec: PdfImageCodec.None } && key.ReductionLevels > 0
            ? ImageBoxReduction.Reduce(data, ImageReduction.RemainingLevels(key.Stream.Dictionary.GetInt32(KnownName.Width), data.Width, key.ReductionLevels))
            : data;
    }

    /// <summary>Empties the cells, soft masks and the pattern paints that hold the cells.</summary>
    private void ClearRecordedPaints()
    {
        // Each cache is made on first use, and clearing must not make one.
        LazyCaches.Clear(ref _cells);
        LazyCaches.Clear(ref _softMasks);
        LazyCaches.Clear(ref _patternPaints);
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
