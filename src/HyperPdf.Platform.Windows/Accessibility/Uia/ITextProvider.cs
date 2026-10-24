// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The <c>ITextProvider</c> interface, which lets a screen reader read an element's text by character, word and line.</summary>
[GeneratedComInterface]
[Guid("3589c92c-63f3-4367-99bb-ada653b77cf2")]
internal partial interface ITextProvider
{
    /// <summary>Gets the selected ranges, or the caret as an empty range.</summary>
    /// <returns>A <c>SAFEARRAY</c> of text ranges.</returns>
    nint GetSelection();

    /// <summary>Gets the ranges that are visible.</summary>
    /// <returns>A <c>SAFEARRAY</c> of text ranges.</returns>
    nint GetVisibleRanges();

    /// <summary>Gets the range that a child element covers.</summary>
    /// <param name="childElement">The child.</param>
    /// <returns>The range.</returns>
    ITextRangeProvider RangeFromChild(IRawElementProviderSimple childElement);

    /// <summary>Gets the range at a point.</summary>
    /// <param name="x">Pixels from the left of the screen.</param>
    /// <param name="y">Pixels from the top of the screen.</param>
    /// <returns>The range.</returns>
    ITextRangeProvider RangeFromPoint(double x, double y);

    /// <summary>Gets the range that covers all of the text.</summary>
    /// <returns>The range.</returns>
    ITextRangeProvider GetDocumentRange();

    /// <summary>Gets how much of the text can be selected.</summary>
    /// <param name="selection">Receives the kind of selection.</param>
    void GetSupportedTextSelection(out SupportedTextSelection selection);
}
