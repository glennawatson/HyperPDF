// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Editing;

/// <summary>The state of one flatten across its pages: what to do, what was done, and the objects to look at afterwards.</summary>
[DebuggerDisplay("PdfFlattenSession: {Options.Mode}, {Options.Targets}")]
internal sealed class PdfFlattenSession
{
    /// <summary>The caches that draw appearances for annotations that have none; made on first use.</summary>
    private PdfRenderCache? _renderCache;

    /// <summary>Initializes a new instance of the <see cref="PdfFlattenSession"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction the edit belongs to.</param>
    /// <param name="options">What to flatten.</param>
    /// <param name="cancellationToken">Cancels the flatten.</param>
    internal PdfFlattenSession(PdfDocument document, PdfEditTransaction transaction, PdfFlattenOptions options, CancellationToken cancellationToken)
    {
        Document = document;
        Transaction = transaction;
        Options = options;
        CancellationToken = cancellationToken;
        PdfAPart = PdfStampConformance.GetPdfAPart(document);
        Structure = PdfFlattenStructure.Create(document, transaction);
    }

    /// <summary>Gets the document.</summary>
    internal PdfDocument Document { get; }

    /// <summary>Gets the transaction the edit belongs to.</summary>
    internal PdfEditTransaction Transaction { get; }

    /// <summary>Gets what to flatten.</summary>
    internal PdfFlattenOptions Options { get; }

    /// <summary>Gets the token that cancels the flatten.</summary>
    internal CancellationToken CancellationToken { get; }

    /// <summary>Gets the part of ISO 19005 the document claims, or 0.</summary>
    internal int PdfAPart { get; }

    /// <summary>Gets the structure of a tagged document, or <see langword="null"/>.</summary>
    internal PdfFlattenStructure? Structure { get; }

    /// <summary>Gets the object numbers of the widgets taken off the pages.</summary>
    internal HashSet<int> RemovedWidgets { get; } = [];

    /// <summary>Gets the object numbers of the annotations taken off the pages.</summary>
    internal HashSet<int> RemovedAnnotations { get; } = [];

    /// <summary>Gets the objects that may be deleted once nothing reaches them.</summary>
    internal List<PdfObjectId> Candidates { get; } = [];

    /// <summary>Gets the annotations that were not drawn.</summary>
    internal List<PdfFlattenSkip> Skipped { get; } = [];

    /// <summary>Gets the appearance streams that were copied to repair them, by the object number of the original.</summary>
    internal Dictionary<int, PdfValue> Repaired { get; } = [];

    /// <summary>Gets or sets the number of pages whose content or annotations changed.</summary>
    internal int PagesChanged { get; set; }

    /// <summary>Gets or sets the number of annotations other than widgets that were drawn.</summary>
    internal int AnnotationsDrawn { get; set; }

    /// <summary>Gets or sets the number of widgets that were drawn.</summary>
    internal int WidgetsDrawn { get; set; }

    /// <summary>Gets or sets the number of appearances made because the annotation had none.</summary>
    internal int AppearancesDrawn { get; set; }

    /// <summary>Gets or sets the number of widgets removed without a drawing.</summary>
    internal int WidgetsRemoved { get; set; }

    /// <summary>Gets or sets the number of pages whose rotation was turned into the content.</summary>
    internal int RotationsBaked { get; set; }

    /// <summary>Gets the form reader and writer of the document.</summary>
    internal PdfForm Form => PdfDocumentForms.GetForm(Document);

    /// <summary>Gets the caches that draw appearances for annotations that have none.</summary>
    internal PdfRenderCache RenderCache => _renderCache ??= PdfDocumentRendering.GetRenderCache(Document);
}
