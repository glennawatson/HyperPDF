// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>An edit of one field that is planned and ready to write: an attribute change or the move of one widget.</summary>
/// <param name="Document">The document.</param>
/// <param name="Node">The field.</param>
/// <param name="Details">The field as it is.</param>
/// <param name="Label">The label of the undo step.</param>
/// <param name="CancellationToken">Cancels the edit.</param>
[DebuggerDisplay("FormFieldEdit: {Label}")]
internal sealed record FormFieldEdit(PdfDocument Document, FormFieldNode Node, PdfFieldDetails Details, string Label, CancellationToken CancellationToken)
{
    /// <summary>Gets the planned attribute change; <see langword="null"/> for a move.</summary>
    internal FormFieldChange? Change { get; init; }

    /// <summary>Gets the widget to move; -1 for an attribute change.</summary>
    internal int WidgetIndex { get; init; } = -1;

    /// <summary>Gets the new rectangle of a move.</summary>
    internal PdfRectangle Bounds { get; init; }
}
