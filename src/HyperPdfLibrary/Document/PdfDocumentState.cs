// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Document;

/// <summary>Owns the document's reusable caches and synchronisation state.</summary>
internal record struct PdfDocumentState
{
    /// <summary>The pages and their indexes by object number; read again after an edit transaction ends.</summary>
    private PdfPageSet? _pageSet;

    /// <summary>The attachment snapshot until structural invalidation.</summary>
    private PdfAttachment[]? _attachments;

    /// <summary>The signature field snapshot until structural invalidation.</summary>
    private PdfSignatureField[]? _signatures;

    /// <summary>The lazily read document form.</summary>
    private PdfForm? _form;

    /// <summary>Parsed page label ranges until structural invalidation.</summary>
    private PageLabelRange[]? _labelRanges;

    /// <summary>Optional content state until structural invalidation.</summary>
    private PdfOptionalContent? _optionalContent;

    /// <summary>The minimum layer version after invalidation.</summary>
    private int _layerVersionFloor;

    /// <summary>The outline snapshot until structural invalidation.</summary>
    private PdfOutlineItem[]? _outline;

    /// <summary>Renderers configured for the output intent.</summary>
    private PdfRenderCache? _intentRenderCache;

    /// <summary>Whether the output intent renderer has been selected.</summary>
    private int _intentDecided;

    /// <summary>The parsed PDF/A claim state.</summary>
    private int _pdfAClaimState;

    /// <summary>Page content edit count used in renderer cache keys.</summary>
    private int _pageContentEdits;

    /// <summary>The normal page renderers.</summary>
    private PdfRenderCache? _renderCache;

    /// <summary>The font data refresh generation for renderer caches.</summary>
    private int _fontDataGeneration;

    /// <summary>The lazily read signature details.</summary>
    private PdfSignatureDetails[]? _signatureDetails;

    /// <summary>The lazily read security information.</summary>
    private PdfSecurityStore? _securityStore;

    /// <summary>The lazily read document revisions.</summary>
    private PdfRevision[]? _revisions;

    /// <summary>The structure tree or the no-tree sentinel.</summary>
    private object? _structureTree;

    /// <summary>Independently published marked-content pages.</summary>
    private PdfMarkedContentPage?[]? _markedContent;

    /// <summary>Extracted text pages until page content invalidation.</summary>
    private PdfTextPageCache? _textPages;

    /// <summary>1 once the owning document is disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="PdfDocumentState"/> struct.</summary>
    public PdfDocumentState()
    {
    }

    /// <summary>Gets the owning document's disposal flag.</summary>
    [UnscopedRef]
    internal ref int Disposed => ref _disposed;

    /// <summary>Gets the cached page set storage for publication and invalidation.</summary>
    [UnscopedRef]
    internal ref PdfPageSet? PageSet => ref _pageSet;

    /// <summary>Gets storage for the attachment snapshot until structural invalidation.</summary>
    [UnscopedRef]
    internal ref PdfAttachment[]? Attachments => ref _attachments;

    /// <summary>Gets storage for the signature field snapshot until structural invalidation.</summary>
    [UnscopedRef]
    internal ref PdfSignatureField[]? Signatures => ref _signatures;

    /// <summary>Gets storage for the lazily read document form.</summary>
    [UnscopedRef]
    internal ref PdfForm? Form => ref _form;

    /// <summary>Gets storage for parsed page label ranges until structural invalidation.</summary>
    [UnscopedRef]
    internal ref PageLabelRange[]? LabelRanges => ref _labelRanges;

    /// <summary>Gets storage for optional content state until structural invalidation.</summary>
    [UnscopedRef]
    internal ref PdfOptionalContent? OptionalContent => ref _optionalContent;

    /// <summary>Gets the minimum layer version above versions held by cached pictures.</summary>
    [UnscopedRef]
    internal ref int LayerVersionFloor => ref _layerVersionFloor;

    /// <summary>Gets storage for the outline snapshot until structural invalidation.</summary>
    [UnscopedRef]
    internal ref PdfOutlineItem[]? Outline => ref _outline;

    /// <summary>Gets storage for renderers configured for the output intent.</summary>
    [UnscopedRef]
    internal ref PdfRenderCache? IntentRenderCache => ref _intentRenderCache;

    /// <summary>Gets storage for the output intent selection state.</summary>
    [UnscopedRef]
    internal ref int IntentDecided => ref _intentDecided;

    /// <summary>Gets storage for the parsed PDF/A claim state.</summary>
    [UnscopedRef]
    internal ref int PdfAClaimState => ref _pdfAClaimState;

    /// <summary>Gets storage for the page content edit count used in renderer cache keys.</summary>
    [UnscopedRef]
    internal ref int PageContentEdits => ref _pageContentEdits;

    /// <summary>Gets the lock that synchronizes tracked page renderers.</summary>
    internal Lock RenderersGate { get; } = new();

    /// <summary>Gets the weakly tracked page renderers for disposal and font refresh.</summary>
    internal List<WeakReference<PdfPageRenderer>> Renderers { get; } = [];

    /// <summary>Gets storage for the normal page renderers.</summary>
    [UnscopedRef]
    internal ref PdfRenderCache? RenderCache => ref _renderCache;

    /// <summary>Gets storage for the font data refresh generation.</summary>
    [UnscopedRef]
    internal ref int FontDataGeneration => ref _fontDataGeneration;

    /// <summary>Gets storage for lazily read signature details.</summary>
    [UnscopedRef]
    internal ref PdfSignatureDetails[]? SignatureDetails => ref _signatureDetails;

    /// <summary>Gets storage for lazily read security information.</summary>
    [UnscopedRef]
    internal ref PdfSecurityStore? SecurityStore => ref _securityStore;

    /// <summary>Gets storage for lazily read document revisions.</summary>
    [UnscopedRef]
    internal ref PdfRevision[]? Revisions => ref _revisions;

    /// <summary>Gets the lock that synchronizes structure tree initialization.</summary>
    internal Lock StructureGate { get; } = new();

    /// <summary>Gets storage for the structure tree or no-tree sentinel.</summary>
    [UnscopedRef]
    internal ref object? StructureTree => ref _structureTree;

    /// <summary>Gets storage for independently published marked-content pages.</summary>
    [UnscopedRef]
    internal ref PdfMarkedContentPage?[]? MarkedContent => ref _markedContent;

    /// <summary>Gets storage for extracted text pages until page content invalidation.</summary>
    [UnscopedRef]
    internal ref PdfTextPageCache? TextPages => ref _textPages;
}
