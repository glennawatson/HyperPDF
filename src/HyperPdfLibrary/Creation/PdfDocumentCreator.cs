// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts.Embedding;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>
/// Creates a new document from text, images, shadings, patterns and existing pages. Add fonts and images, draw pages with
/// <see cref="BeginPage(float, float)"/>, and save. Fonts used by new text are embedded and cut down to the glyphs used;
/// a tagged document gets a structure tree whose marked content ids and parent tree are written for the pages. Objects are
/// kept in a <see cref="PdfDocumentBuilder"/> and written with a classic cross-reference table. Not thread-safe.
/// </summary>
[DebuggerDisplay("PdfDocumentCreator: {_builder.PageCount} pages, tagged {Tags != null}")]
public sealed class PdfDocumentCreator
{
    /// <summary>The entries of a page dictionary.</summary>
    private const int PageEntries = 8;

    /// <summary>The document being built.</summary>
    private readonly PdfDocumentBuilder _builder = new();

    /// <summary>The options.</summary>
    private readonly PdfDocumentCreatorOptions _options;

    /// <summary>The first font, kept without a collection when it is the only one.</summary>
    private PdfCanvasFont? _firstFont;

    /// <summary>Fonts after the first, for checking that all are embedded.</summary>
    private List<PdfCanvasFont>? _otherFonts;

    /// <summary>The importers of pages from other documents, one for each source so shared objects are copied once.</summary>
    private Dictionary<PdfDocument, PdfPageImporter>? _importers;

    /// <summary>The resource set of the last page that finished, kept for the next page to reuse.</summary>
    private PdfPageResources? _spareResources;

    /// <summary>The report once the document is finished.</summary>
    private PdfCreationReport? _report;

    /// <summary>The number of pages copied from other documents.</summary>
    private int _importedPages;

    /// <summary>Initializes a new instance of the <see cref="PdfDocumentCreator"/> class.</summary>
    /// <param name="options">How the document is described and structured.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    public PdfDocumentCreator(PdfDocumentCreatorOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _builder.Version = options.UaClaim == PdfUaConformance.Part1 ? PdfHeaderVersion.Pdf17 : PdfHeaderVersion.Pdf20;
        if (options.Tagged)
        {
            Tags = new(_builder, options.UaClaim == PdfUaConformance.Part2);
        }
    }

    /// <summary>Gets the structure tree, or <see langword="null"/> when the document is not tagged.</summary>
    public PdfTagTree? Tags { get; }

    /// <summary>Gets the document's objects, which fonts, images, shadings and patterns are added to.</summary>
    public IPdfObjectSink Sink => _builder;

    /// <summary>Gets the number of pages added so far.</summary>
    public int PageCount => _builder.PageCount;

    /// <summary>Gets the document being built.</summary>
    internal PdfDocumentBuilder Builder => _builder;

    /// <summary>Gets or sets the content size of the last page that ended, which the next page's buffer starts at so it does not grow and copy; zero before any page ends.</summary>
    internal int ContentCapacityHint { get; set; }

    /// <summary>Starts embedding a font; text drawn with it is shaped and its glyphs are recorded.</summary>
    /// <param name="source">The font, opened with <see cref="PdfFontSource.Open(byte[], int)"/>.</param>
    /// <returns>The font to draw with.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is finished.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfCanvasFont EmbedFont(PdfFontSource source) => EmbedFont(source, _options.FontOptions);

    /// <summary>Starts embedding a font; text drawn with it is shaped and its glyphs are recorded.</summary>
    /// <param name="source">The font, opened with <see cref="PdfFontSource.Open(byte[], int)"/>.</param>
    /// <param name="options">How the font is embedded and how text is shaped.</param>
    /// <returns>The font to draw with.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is finished.</exception>
    public PdfCanvasFont EmbedFont(PdfFontSource source, PdfEmbeddedFontOptions options)
    {
        ThrowIfFinished();
        var font = PdfCanvasFont.FromEmbedded(PdfEmbeddedFont.Create(_builder, source, options));
        TrackFont(font);
        return font;
    }

    /// <summary>Adds one of the 14 built-in fonts. A document that uses one cannot claim PDF/UA, which needs every font embedded.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The font to draw with.</returns>
    /// <exception cref="InvalidOperationException">The document is finished.</exception>
    public PdfCanvasFont AddBuiltInFont(AppearanceFont font)
    {
        ThrowIfFinished();
        var added = PdfCanvasFont.FromBuiltIn(_builder, font);
        TrackFont(added);
        return added;
    }

    /// <summary>Adds a JPEG, keeping its bytes.</summary>
    /// <param name="jpeg">The JPEG file's bytes.</param>
    /// <param name="options">The colour space and interpolation.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentException">The bytes are not a JPEG that PDF can carry.</exception>
    /// <exception cref="InvalidOperationException">The document is finished.</exception>
    public PdfCreatedImage AddJpeg(ReadOnlySpan<byte> jpeg, PdfImageOptions options)
    {
        ThrowIfFinished();
        return PdfImageWriter.AddJpeg(_builder, jpeg, options);
    }

    /// <summary>Adds decoded pixels as a lossless image.</summary>
    /// <param name="image">The pixels.</param>
    /// <param name="options">The colour space, interpolation and stencil choice.</param>
    /// <param name="cancellationToken">Cancels the encoding between its steps.</param>
    /// <returns>The image.</returns>
    /// <exception cref="ArgumentException">The pixels do not fill the image.</exception>
    /// <exception cref="InvalidOperationException">The document is finished.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public PdfCreatedImage AddRaster(PdfRasterImage image, PdfImageOptions options, CancellationToken cancellationToken)
    {
        ThrowIfFinished();
        return PdfImageWriter.AddRaster(_builder, image, options, cancellationToken);
    }

    /// <summary>Starts a page.</summary>
    /// <param name="width">The page width in points.</param>
    /// <param name="height">The page height in points.</param>
    /// <returns>The canvas to draw on; call <see cref="PdfPageCanvas.Finish"/> to add the page.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A size is not positive.</exception>
    /// <exception cref="InvalidOperationException">The document is finished.</exception>
    public PdfPageCanvas BeginPage(float width, float height)
    {
        ThrowIfFinished();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        return new(this, _builder.Reserve(), width, height);
    }

    /// <summary>Copies a page of another document as the next page. The copy keeps the page's content, resources, boxes, rotation and annotations, and is not tagged.</summary>
    /// <param name="source">The document to copy from.</param>
    /// <param name="pageIndex">The zero-based index of the page.</param>
    /// <returns>The new page's object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page of <paramref name="source"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is finished.</exception>
    public PdfObjectId AddPage(PdfDocument source, int pageIndex)
    {
        ThrowIfFinished();
        ArgumentNullException.ThrowIfNull(source);
        ArgumentOutOfRangeException.ThrowIfNegative(pageIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(pageIndex, source.PageCount);
        var importers = _importers ??= [with(ReferenceEqualityComparer.Instance)];
        ref var importer = ref CollectionsMarshal.GetValueRefOrAddDefault(importers, source, out _);
        importer ??= new(_builder, source);
        _importedPages++;
        return importer.ImportPage(PdfDocumentPages.GetPage(source, pageIndex), PdfAnnotationFilter.All);
    }

    /// <summary>Copies a page of another document as the next page.</summary>
    /// <param name="source">The document to copy from.</param>
    /// <param name="pageIndex">The zero-based index of the page.</param>
    /// <param name="cancellationToken">Cancels before the page is copied.</param>
    /// <returns>The new page's object.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="pageIndex"/> is not a page of <paramref name="source"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is finished.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public ValueTask<PdfObjectId> AddPageAsync(PdfDocument source, int pageIndex, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(AddPage(source, pageIndex));
    }

    /// <summary>Finishes the document: embeds the fonts, writes the structure tree and the metadata. Calling it again returns the same report.</summary>
    /// <returns>What the document contains and what it could not promise.</returns>
    public PdfCreationReport Finish()
    {
        if (_report is not null)
        {
            return _report;
        }

        var notes = new List<string>();
        PdfEmbeddedFont.FinishAll(_builder);
        var claim = DecideClaim(notes);
        var created = (_options.Clock ?? TimeProvider.System).GetUtcNow();
        WriteCatalog(created, claim);
        _report = new(_builder.PageCount, Tags is not null, claim != PdfUaConformance.None, [.. notes]);
        return _report;
    }

    /// <summary>Finishes the document and writes it into a new array.</summary>
    /// <returns>The file bytes.</returns>
    /// <exception cref="PdfException">A value cannot be written.</exception>
    public byte[] ToArray()
    {
        _ = Finish();
        return _builder.ToArray();
    }

    /// <summary>Finishes the document and writes it to a stream.</summary>
    /// <param name="destination">The stream.</param>
    /// <returns>What the document contains and what it could not promise.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">A value cannot be written.</exception>
    public PdfCreationReport Save(Stream destination)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var report = Finish();
        _builder.Save(destination);
        return report;
    }

    /// <summary>Finishes the document and writes it to a stream, writing to the stream asynchronously.</summary>
    /// <param name="destination">The stream.</param>
    /// <param name="cancellationToken">Cancels the write to the stream.</param>
    /// <returns>What the document contains and what it could not promise.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="destination"/> is <see langword="null"/>.</exception>
    /// <exception cref="PdfException">A value cannot be written.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public async ValueTask<PdfCreationReport> SaveAsync(Stream destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var report = Finish();
        await _builder.SaveAsync(destination, cancellationToken).ConfigureAwait(false);
        return report;
    }

    /// <summary>Gets a resource set for a page, reusing the one a finished page gave back.</summary>
    /// <returns>An empty resource set.</returns>
    internal PdfPageResources RentResources()
    {
        var spare = _spareResources;
        if (spare is null)
        {
            return new(_builder);
        }

        _spareResources = null;
        return spare;
    }

    /// <summary>Takes back the resources of a page that is finished or abandoned, so the next page reuses them.</summary>
    /// <param name="resources">The page's resources, or <see langword="null"/>.</param>
    internal void ReturnResources(PdfPageResources? resources)
    {
        if (resources is null)
        {
            return;
        }

        resources.Clear();
        _spareResources = resources;
    }

    /// <summary>Writes a finished page's content and dictionary and adds the page.</summary>
    /// <param name="page">The page object reserved by <see cref="BeginPage(float, float)"/>.</param>
    /// <param name="width">The page width.</param>
    /// <param name="height">The page height.</param>
    /// <param name="resources">The page's resources, or <see langword="null"/> when nothing was added to them.</param>
    /// <param name="content">The content stream's bytes.</param>
    internal void CompletePage(PdfObjectId page, float width, float height, PdfPageResources? resources, byte[] content)
    {
        ThrowIfFinished();
        var owner = _builder.Owner;
        var dictionary = new PdfDictionary(owner, PageEntries);
        dictionary.Set(KnownName.MediaBox, PdfValue.FromArray(PdfArray.FromNumbers(owner, [0F, 0F, width, height])));
        dictionary.Set(KnownName.Resources, PdfValue.FromDictionary(resources?.ToDictionary() ?? new PdfDictionary(owner)));
        var stream = new PdfStream(new(owner), content);
        dictionary.Set(KnownName.Contents, PdfValue.FromReference(_builder.Add(PdfValue.FromStream(stream))));
        if (Tags is not null)
        {
            dictionary.Set(KnownName.Tabs, PdfValue.FromName(_builder.Names.Intern("S")));
            if (Tags.TryGetStructParents(page, out var key))
            {
                dictionary.Set(KnownName.StructParents, PdfValue.FromInteger(key));
            }
        }

        _ = _builder.AddPage(page, dictionary);
    }

    /// <summary>Adds a reason when a condition holds.</summary>
    /// <param name="reasons">Receives the reason.</param>
    /// <param name="condition">Whether the reason applies.</param>
    /// <param name="reason">The reason.</param>
    private static void AddIf(List<string> reasons, bool? condition, string reason)
    {
        if (condition == true)
        {
            reasons.Add(reason);
        }
    }

    /// <summary>Retains a font so the PDF/UA claim can check it when the document is finished.</summary>
    /// <param name="font">The font used by this document.</param>
    private void TrackFont(PdfCanvasFont font)
    {
        if (_firstFont is null)
        {
            _firstFont = font;
        }
        else
        {
            (_otherFonts ??= []).Add(font);
        }
    }

    /// <summary>Decides which PDF/UA part, if any, the document can claim, and notes why it cannot claim one.</summary>
    /// <param name="notes">Receives the reasons.</param>
    /// <returns>The part to claim, or <see cref="PdfUaConformance.None"/>.</returns>
    private PdfUaConformance DecideClaim(List<string> notes)
    {
        if (_options.UaClaim == PdfUaConformance.None)
        {
            return PdfUaConformance.None;
        }

        var reasons = new List<string>();
        AddIf(reasons, Tags is null, "The document is not tagged.");
        AddIf(reasons, string.IsNullOrWhiteSpace(_options.Title), "The document has no title.");
        AddIf(reasons, string.IsNullOrWhiteSpace(_options.Language), "The document has no language.");
        AddIf(reasons, _firstFont is { IsEmbedded: false } || _otherFonts?.Exists(static font => !font.IsEmbedded) == true, "A built-in font is used, and PDF/UA needs every font embedded.");
        AddIf(reasons, _importedPages > 0, "Pages copied from other documents are not tagged by this document.");
        AddIf(reasons, Tags?.CountFiguresWithoutDescription() > 0, "A figure has no alternative text.");
        if (reasons.Count == 0)
        {
            return _options.UaClaim;
        }

        notes.Add($"The PDF/UA claim was left out. {string.Join(' ', reasons)}");
        return PdfUaConformance.None;
    }

    /// <summary>Writes the catalog entries, the information dictionary and the metadata.</summary>
    /// <param name="created">The creation date.</param>
    /// <param name="claim">The PDF/UA part claimed.</param>
    private void WriteCatalog(DateTimeOffset created, PdfUaConformance claim)
    {
        var catalog = _builder.CatalogEntries;
        if (!string.IsNullOrWhiteSpace(_options.Language))
        {
            catalog.Set(KnownName.Lang, PdfValue.FromString(PdfText.Encode(_options.Language)));
        }

        if (Tags is { } tags)
        {
            catalog.Set(KnownName.StructTreeRoot, PdfValue.FromReference(tags.Finish()));
            var markInfo = new PdfDictionary(_builder.Owner);
            markInfo.Set(_builder.Names.Intern("Marked"), PdfValue.FromBoolean(true));
            catalog.Set(KnownName.MarkInfo, PdfValue.FromDictionary(markInfo));
        }

        if (!string.IsNullOrWhiteSpace(_options.Title))
        {
            var preferences = new PdfDictionary(_builder.Owner);
            preferences.Set(_builder.Names.Intern("DisplayDocTitle"), PdfValue.FromBoolean(true));
            catalog.Set(KnownName.ViewerPreferences, PdfValue.FromDictionary(preferences));
        }

        catalog.Set(KnownName.Metadata, PdfValue.FromReference(CreatedMetadata.AddXmp(_builder, _options, created, (int)claim)));
        _builder.InfoId = CreatedMetadata.AddInfo(_builder, _options, created);
    }

    /// <summary>Throws when the document is finished.</summary>
    /// <exception cref="InvalidOperationException">The document is finished.</exception>
    private void ThrowIfFinished()
    {
        if (_report is not null)
        {
            throw new InvalidOperationException("The document is finished and cannot change.");
        }
    }
}
