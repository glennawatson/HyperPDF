// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms;
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
    /// <param name="bounds">The widget's bounds in viewer space.</param>
    /// <returns>The field.</returns>
    internal static FormField ToField(
            PdfFormWidget widget,
            PdfRectangle bounds) =>
            new(
            widget.PageIndex,
            widget.Index,
            widget.Name,
            (FormFieldKind)(int)widget.Type,
            LinkTargets.ToPageRect(bounds),
            widget.Value,
            widget.IsChecked,
            widget.Options,
            widget.SelectedOption,
            widget.IsReadOnly,
            widget.IsRequired,
            widget.IsMultiline)
            { MaxLength = widget.MaxLength, IsComb = widget.IsComb, FontSize = widget.FontSize };
}
