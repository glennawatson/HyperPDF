// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
namespace HyperPdf.Engine;

/// <summary>Implements Forms over the document's owned state.</summary>
internal static class HyperPdfForms
{
    /// <summary>Gets HasManagedEdits.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The current value.</returns>
    internal static bool GetHasManagedEdits(HyperPdfDocument self) => !self.IsDisposed && StoreEditing.HasEdits(self.Document.Objects);

    /// <summary>Converts a library widget to the viewer's field.</summary>
    /// <param name="widget">The widget.</param>
    /// <param name="page">The page the widget is on; its bounds and font size are converted to physical points.</param>
    /// <returns>The field, with bounds in physical viewer space.</returns>
    internal static FormField ToField(
            PdfFormWidget widget,
            PdfPage page) =>
            new(
            widget.PageIndex,
            widget.Index,
            widget.Name,
            (FormFieldKind)(int)widget.Type,
            LinkTargets.ToPageRect(page.ToPhysicalRectangle(widget.Bounds)),
            widget.Value,
            widget.IsChecked,
            widget.Options,
            widget.SelectedOption,
            widget.IsReadOnly,
            widget.IsRequired,
            widget.IsMultiline)
            { MaxLength = widget.MaxLength, IsComb = widget.IsComb, FontSize = widget.FontSize * page.UserUnit };
}
