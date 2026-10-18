// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Optimizing;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Drawing;

/// <summary>The drawing services shared by documents opened with this instance.</summary>
[DebuggerDisplay("Drawing {ConfiguredBackend}, fonts {Fonts}, images {Images}")]
public sealed class PdfDrawingServices
{
    /// <summary>The cleaned text extraction states retained between documents for this host.</summary>
    private const int TextBuildSlots = 4;

    /// <summary>The font resources shared by documents using this service.</summary>
    private PdfFontResources? _fontResources;

    /// <summary>Initializes a new instance of the <see cref="PdfDrawingServices"/> class.</summary>
    /// <param name="registration">The drawing backend and providers.</param>
    public PdfDrawingServices(PdfRenderBackendRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        ConfiguredBackend = registration.Backend;
        Fonts = registration.Fonts;
        Images = registration.Images;
        UnsupportedImageDecoder = registration.UnsupportedImageDecoder;
    }

    /// <summary>Gets the optional font provider.</summary>
    public IPdfFontProvider? Fonts { get; }

    /// <summary>Gets the optional image codec.</summary>
    public IPdfImageCodec? Images { get; }

    /// <summary>Gets the decoder for images not supported by the library.</summary>
    public Func<PdfStream, CancellationToken, IPdfRenderImage?>? UnsupportedImageDecoder { get; }

    /// <summary>Gets a decoder scratch cache shared by documents using this service instance, or null for document-owned caches.</summary>
    public ScratchPools? SharedScratchPools { get; init; }

    /// <summary>
    /// Gets whether the engine may download font data that is not inside the library; the default is
    /// <see cref="PdfFontDataMode.OnDemand"/>. Set it before the services are used to open a document.
    /// </summary>
    public PdfFontDataMode FontDataMode { get; init; }

    /// <summary>Gets the drawing backend.</summary>
    /// <exception cref="InvalidOperationException">No backend was supplied.</exception>
    public IPdfRenderBackend Backend => ConfiguredBackend
        ?? throw new InvalidOperationException("Supply a drawing backend in PdfOpenOptions.DrawingServices before rendering PDF pages.");

    /// <summary>Gets the optional drawing backend for rendering resources.</summary>
    internal IPdfRenderBackend? ConfiguredBackend { get; }

    /// <summary>Gets reusable default open options bound to these host services.</summary>
    internal PdfOpenOptions PasswordlessOpenOptions
    {
        get
        {
            if (Volatile.Read(ref field) is { } existing)
            {
                return existing;
            }

            var created = new PdfOpenOptions { DrawingServices = this };
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets the idle content interpreters shared by documents using these host services.</summary>
    internal ReusableSlots<ContentInterpreter> Interpreters
    {
        get
        {
            if (Volatile.Read(ref field) is { } existing)
            {
                return existing;
            }

            var created = new ReusableSlots<ContentInterpreter>();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets idle progressive recordings shared by documents using these host services.</summary>
    internal ReusableSlots<ProgressiveRecording> ProgressiveRecordings
    {
        get
        {
            if (Volatile.Read(ref field) is { } existing)
            {
                return existing;
            }

            var created = new ReusableSlots<ProgressiveRecording>();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets the page rectangle paths shared by documents using these host services.</summary>
    internal PageClips PageClips
    {
        get
        {
            if (Volatile.Read(ref field) is { } existing)
            {
                return existing;
            }

            var created = new PageClips();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets the bounded text extraction state pool for documents using these host services.</summary>
    internal ReusableSlots<TextPageBuildState> TextBuildStates
    {
        get
        {
            if (Volatile.Read(ref field) is { } existing)
            {
                return existing;
            }

            var created = new ReusableSlots<TextPageBuildState>(TextBuildSlots);
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets cleaned marked-content recorders shared only by documents using these host services.</summary>
    internal ReusableSlots<MarkedContentRecorder> MarkedContentRecorders
    {
        get
        {
            if (Volatile.Read(ref field) is { } existing)
            {
                return existing;
            }

            var created = new ReusableSlots<MarkedContentRecorder>();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets the transport that font data downloads use, or <see langword="null"/> for the default; tests supply one.</summary>
    internal HttpMessageHandler? FontDataHandler { get; init; }

    /// <summary>Gets the font match cache for this service instance.</summary>
    internal SystemFontMatcher FontMatcher
    {
        get
        {
            var existing = Volatile.Read(ref field);
            if (existing is not null)
            {
                return existing;
            }

            var created = new SystemFontMatcher(Fonts, FontResources);
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets the font resources selected by this service.</summary>
    internal PdfFontResources FontResources
    {
        get
        {
            var cached = Volatile.Read(ref _fontResources);
            if (cached is not null)
            {
                return cached;
            }

            var created = new PdfFontResources(FontDataMode, FontDataHandler);
            return Interlocked.CompareExchange(ref _fontResources, created, null) ?? created;
        }
    }

    /// <summary>Gets the font resource generation without making an empty cache.</summary>
    internal int FontResourceGeneration => Volatile.Read(ref _fontResources)?.Generation ?? 0;

    /// <summary>Gets reusable optimiser state scoped to this registered service instance.</summary>
    internal OptimizerResources Optimization
    {
        get
        {
            var existing = Volatile.Read(ref field);
            if (existing is not null)
            {
                return existing;
            }

            var created = new OptimizerResources();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Gets the ICC transforms shared by documents opened with this service instance.</summary>
    internal IccProfileCache Profiles
    {
        get
        {
            var existing = Volatile.Read(ref field);
            if (existing is not null)
            {
                return existing;
            }

            var created = new IccProfileCache();
            return Interlocked.CompareExchange(ref field, created, null) ?? created;
        }
    }

    /// <summary>Lists the font data an offline service needed but did not have, so a caller can add it to the font data folder.</summary>
    /// <returns>
    /// The group and file name of each, such as <c>CMaps/90ms-RKSJ-H.bin</c>, in ordinal order; empty when nothing was
    /// missing or the mode is <see cref="PdfFontDataMode.OnDemand"/>.
    /// </returns>
    public string[] GetMissingFontData() => Volatile.Read(ref _fontResources)?.GetMissing() ?? [];

    /// <summary>Gets a dictionary's document services, or empty services for a detached dictionary.</summary>
    /// <param name="dictionary">The dictionary owned by a document.</param>
    /// <returns>The drawing services.</returns>
    internal static PdfDrawingServices For(PdfDictionary dictionary) => dictionary.Owner?.Context?.DrawingServices ?? new(new());
}
