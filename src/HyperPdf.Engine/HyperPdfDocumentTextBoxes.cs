// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Text;
namespace HyperPdf.Engine;

/// <summary>Implements DocumentTextBoxes over the document's owned state.</summary>
internal static class HyperPdfDocumentTextBoxes
{
    /// <summary>Writes a text box.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the box.</param>
    /// <param name="wrapWidth">The width lines wrap at, in points, or 0 to break lines only where the text does.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="format">How the text looks.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    internal static int AddTextBox(HyperPdfDocument self, int pageIndex, PagePoint location, float wrapWidth, string text, TextFormat format)
    {
        lock (self.EditGate)
        {
            using var access = HyperPdfNavigation.EnterPageWrite(self);
            return HyperPdfEditing.Added(self, pageIndex, HyperPdfAnnotationTextBoxes.AddTextBox(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, location, wrapWidth, text, format));
        }
    }

    /// <summary>Reads a text box's text, look and place, from this viewer or another program.</summary>
    /// <param name="self">The owning document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>The content, or <see langword="null"/> when the annotation is not editable text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TextBoxContent? GetTextBox(HyperPdfDocument self, int pageIndex, int index)
    {
        using var access = HyperPdfNavigation.EnterPageRead(self);
        return HyperPdfAnnotationTextBoxes.GetTextBox(HyperPdfAnnotationStateAccess.GetAnnotations(self), pageIndex, index);
    }

    /// <summary>
    /// Gets how far below a text box's top its first baseline is written, in points, in the font the text would be
    /// written in. An editor showing the text lines its own baseline up with this so the text does not move when kept.
    /// </summary>
    /// <param name="self">The owning document.</param>
    /// <param name="text">The text, which picks a fallback font when the chosen one lacks some of it.</param>
    /// <param name="format">How the text looks.</param>
    /// <returns>The baseline's depth, or <see cref="F:System.Single.NaN"/> when no font can be loaded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float GetFirstBaseline(
        HyperPdfDocument self,
        string text,
        TextFormat format) => HyperPdfAnnotationTextBoxes.GetFirstBaseline(HyperPdfAnnotationStateAccess.GetAnnotations(self), text, format);
}
