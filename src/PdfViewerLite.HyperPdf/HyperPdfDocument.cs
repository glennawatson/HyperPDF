// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.Core.Attachments;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Signatures;
namespace PdfViewerLite.HyperPdf;

/// <summary>
/// A document opened with HyperPDF. The managed library answers every call, draws every page and holds every edit.
/// Safe to call from any thread.
/// </summary>
[DebuggerDisplay("HyperPdfDocument: {FilePath}")]
public sealed class HyperPdfDocument : IDocument, IHyperPdfGpuRenderer
{
    /// <summary>The managed document.</summary>
    private readonly PdfDocument _document;

    /// <summary>Allows concurrent page reads and excludes them while page structure changes.</summary>
    private readonly ReaderWriterLockSlim _pageAccess = new(LockRecursionPolicy.SupportsRecursion);

    /// <summary>Serialises edits and saves, so each edit and the caches it drops change together.</summary>
    private readonly Lock _editGate = new();

    /// <summary>The native annotation editor, once made.</summary>
    private HyperPdfAnnotations? _annotations;

    /// <summary>The attachments, read once; viewing never adds or removes them.</summary>
    private DocumentAttachment[]? _attachments;

    /// <summary>The signatures, read once; they cannot change while the document is open.</summary>
    private RawSignature[]? _signatures;

    /// <summary>The registered page callers and the bit that stops registration after cleanup.</summary>
    private int _pageAccessLeases;

    /// <summary>The page sizes, replaced after editing the page tree.</summary>
    private PageSize[] _pageSizes;

    /// <summary>Owns page edit history and imported document lifetimes after the first page action.</summary>
    private HyperPdfPageManager? _pageManager;

    /// <summary>The outline, read on first use.</summary>
    private OutlineNode[]? _outline;

    /// <summary>The links of each page, read on first use.</summary>
    private PageLink[]?[]? _links;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>The number of edits made since opening.</summary>
    private long _editVersion;

    /// <summary>The edit version the last successful save wrote.</summary>
    private long _savedVersion;

    /// <summary>The tint over fillable fields, packed by <see cref="HyperPdfFormRuntime.Pack"/> so one atomic write publishes it.</summary>
    private long _highlight = HyperPdfFormRuntime.Pack(FormHighlight.Default);

    /// <summary>The renderer, made on first use and replaced after each edit so no picture of the old page is replayed.</summary>
    private PdfPageRenderer? _renderer;

    /// <summary>The optional services, created and retained under the edit gate.</summary>
    private object?[]? _featureSlots;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfDocument"/> class.</summary>
    /// <param name="document">The managed document.</param>
    /// <param name="filePath">The file path.</param>
    internal HyperPdfDocument(PdfDocument document, string filePath)
    {
        _document = document;
        FilePath = filePath;
        _pageSizes = new PageSize[document.PageCount];
        for (var i = 0; i < _pageSizes.Length; i++)
        {
            var page = PdfDocumentPages.GetPage(document, i);
            _pageSizes[i] = new(page.Width, page.Height);
        }
    }

    /// <inheritdoc/>
    public string FilePath { get; }

    /// <inheritdoc/>
    public int PageCount => Volatile.Read(ref _pageSizes).Length;

    /// <inheritdoc/>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets or sets the owned AnnotationState state.</summary>
    internal ref HyperPdfAnnotations? AnnotationState => ref _annotations;

    /// <summary>Gets or sets the owned Attachments state.</summary>
    internal ref DocumentAttachment[]? Attachments => ref _attachments;

    /// <summary>Gets or sets the owned Signatures state.</summary>
    internal ref RawSignature[]? Signatures => ref _signatures;

    /// <summary>Gets the owned PageAccess state.</summary>
    internal ReaderWriterLockSlim PageAccess => _pageAccess;

    /// <summary>Gets or sets the owned PageAccessLeases state.</summary>
    internal ref int PageAccessLeases => ref _pageAccessLeases;

    /// <summary>Gets or sets the owned PageSizes state.</summary>
    internal ref PageSize[] PageSizes => ref _pageSizes;

    /// <summary>Gets or sets the owned PageManager state.</summary>
    internal ref HyperPdfPageManager? PageManager => ref _pageManager;

    /// <summary>Gets or sets the owned Outline state.</summary>
    internal ref OutlineNode[]? Outline => ref _outline;

    /// <summary>Gets or sets the owned Links state.</summary>
    internal ref PageLink[]?[]? Links => ref _links;

    /// <summary>Gets the managed document.</summary>
    internal PdfDocument Document => _document;

    /// <summary>Gets the owned EditGate state.</summary>
    internal Lock EditGate => _editGate;

    /// <summary>Gets or sets the owned EditVersion state.</summary>
    internal ref long EditVersion => ref _editVersion;

    /// <summary>Gets or sets the owned SavedVersion state.</summary>
    internal ref long SavedVersion => ref _savedVersion;

    /// <summary>Gets or sets the owned Highlight state.</summary>
    internal ref long Highlight => ref _highlight;

    /// <summary>Gets or sets the owned RendererState state.</summary>
    internal ref PdfPageRenderer? RendererState => ref _renderer;

    /// <summary>Gets or sets the cached feature services.</summary>
    internal ref object?[]? FeatureSlots => ref _featureSlots;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PageSize[] GetPageSizes() => HyperPdfNavigation.GetPageSizes(this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public DocumentMetadata GetMetadata() => HyperPdfNavigation.GetMetadata(this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string? GetPageLabel(int pageIndex) => HyperPdfNavigation.GetPageLabel(
            this,
            pageIndex);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<OutlineNode> GetOutline() => HyperPdfNavigation.GetOutline(this);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<PageLink> GetLinks(int pageIndex) => HyperPdfNavigation.GetLinks(
            this,
            pageIndex);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_editGate)
        {
            try
            {
                using (HyperPdfNavigation.EnterPageWrite(this))
                {
                    _pageManager?.Dispose();
                    HyperPdfRendering.ResetRenderer(this);
                    _document.Dispose();
                }
            }
            finally
            {
                HyperPdfPageAccess.Close(_pageAccess, ref _pageAccessLeases);
            }
        }
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask PreparePageAsync(int pageIndex, CancellationToken cancellationToken) => HyperPdfRendering.PreparePageAsync(
            this,
            pageIndex,
            cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Render(in PageRenderInfo info, RenderTarget target) => HyperPdfRendering.Render(
            this,
            in info,
            target);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    bool IHyperPdfGpuRenderer.Render(in PageRenderInfo info, HyperPdfLibrary.Drawing.IPdfRenderTarget target) => HyperPdfRendering.RenderGpu(this, info, target);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Prepare(in PageRenderInfo info, CancellationToken cancellationToken) => HyperPdfRendering.Prepare(this, info, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ValueTask<bool> PrepareAsync(PageRenderInfo info, CancellationToken cancellationToken) => HyperPdfRendering.PrepareAsync(this, info, cancellationToken);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCharacterCount(int pageIndex) => HyperPdfText.GetCharacterCount(
            this,
            pageIndex);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCharacterIndexAt(int pageIndex, PagePoint point, float tolerance) => HyperPdfText.GetCharacterIndexAt(
            this,
            pageIndex,
            point,
            tolerance);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetText(int pageIndex, int start, int count) => HyperPdfText.GetText(
            this,
            pageIndex,
            start,
            count);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetTextBounds(int pageIndex, int start, int count, List<PageRect> output) => HyperPdfText.GetTextBounds(
            this,
            pageIndex,
            start,
            count,
            output);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Find(int pageIndex, string query, SearchOptions options, List<TextMatch> output) => HyperPdfText.Find(
            this,
            pageIndex,
            query,
            options,
            output);

    /// <summary>Gets an optional feature while the document is open.</summary>
    /// <param name="featureType">The feature interface type.</param>
    /// <returns>The stable feature, or null when unsupported.</returns>
    /// <exception cref="ArgumentNullException">The feature type is null.</exception>
    /// <exception cref="ObjectDisposedException">The document is closed.</exception>
    public object? GetFeature(Type featureType)
    {
        ArgumentNullException.ThrowIfNull(featureType);
        return HyperPdfFeatureRegistry.Get(this, featureType);
    }
}
