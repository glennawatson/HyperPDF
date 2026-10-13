// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>What a flatten drew, removed and left alone.</summary>
/// <param name="PagesChanged">The number of pages whose content or annotations changed.</param>
/// <param name="AnnotationsDrawn">The number of annotations drawn into page content and removed, form field widgets not counted.</param>
/// <param name="WidgetsDrawn">The number of form field widgets drawn into page content and removed.</param>
/// <param name="AppearancesDrawn">The number of appearances drawn from field values or default shapes because the annotation had none.</param>
/// <param name="WidgetsRemoved">The number of form field widgets removed without drawing, as hidden or empty fields.</param>
/// <param name="FieldsRemoved">The number of form fields removed from the document's field tree.</param>
/// <param name="FormRemoved">Whether the document's <c>/AcroForm</c> was removed because no field was left.</param>
/// <param name="XfaRemoved">Whether the form's XFA data was removed, because it no longer matches the fields.</param>
/// <param name="TaggedContent">The number of drawings bound to a structure element through marked content.</param>
/// <param name="Artifacts">The number of drawings marked as artifacts in a tagged document.</param>
/// <param name="RotationsBaked">The number of pages whose rotation was turned into the content.</param>
/// <param name="Skipped">The annotations that were not drawn, and why.</param>
[DebuggerDisplay("PdfFlattenReport: {AnnotationsDrawn} annotations, {WidgetsDrawn} widgets, {Skipped.Count} skipped")]
public sealed record PdfFlattenReport(
    int PagesChanged,
    int AnnotationsDrawn,
    int WidgetsDrawn,
    int AppearancesDrawn,
    int WidgetsRemoved,
    int FieldsRemoved,
    bool FormRemoved,
    bool XfaRemoved,
    int TaggedContent,
    int Artifacts,
    int RotationsBaked,
    IReadOnlyList<PdfFlattenSkip> Skipped)
{
    /// <summary>Gets the total number of annotations drawn into page content.</summary>
    public int TotalDrawn => AnnotationsDrawn + WidgetsDrawn;
}
