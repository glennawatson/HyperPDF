// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Drawing;

/// <summary>The drawing services shared by documents opened with this instance.</summary>
[DebuggerDisplay("Drawing {ConfiguredBackend}, fonts {Fonts}, images {Images}")]
public sealed class PdfDrawingServices
{
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

    /// <summary>Gets the drawing backend.</summary>
    /// <exception cref="InvalidOperationException">No backend was supplied.</exception>
    public IPdfRenderBackend Backend => ConfiguredBackend
        ?? throw new InvalidOperationException("Supply a drawing backend in PdfOpenOptions.DrawingServices before rendering PDF pages.");

    /// <summary>Gets the optional drawing backend for rendering resources.</summary>
    internal IPdfRenderBackend? ConfiguredBackend { get; }

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

            var created = new SystemFontMatcher(Fonts);
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

    /// <summary>Gets a decoder scratch cache shared by documents using this service instance, or null for document-owned caches.</summary>
    public ScratchPools? SharedScratchPools { get; init; }

    /// <summary>Gets a dictionary's document services, or empty services for a detached dictionary.</summary>
    /// <param name="dictionary">The dictionary owned by a document.</param>
    /// <returns>The drawing services.</returns>
    internal static PdfDrawingServices For(PdfDictionary dictionary) => dictionary.Owner?.Context?.DrawingServices ?? new(new());
}
