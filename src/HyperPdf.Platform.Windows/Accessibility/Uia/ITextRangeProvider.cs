// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using HyperPdf.Core.Accessibility;

namespace HyperPdf.Platform.Windows.Accessibility.Uia;

/// <summary>The <c>ITextRangeProvider</c> interface, a stretch of text that a screen reader moves and reads.</summary>
[GeneratedComInterface]
[Guid("5347ad7b-c355-46f8-aff5-909033582f63")]
internal partial interface ITextRangeProvider
{
    /// <summary>Makes a copy of the range.</summary>
    /// <returns>The copy.</returns>
    ITextRangeProvider Clone();

    /// <summary>Checks whether another range covers the same text.</summary>
    /// <param name="range">The other range.</param>
    /// <returns>Whether the ranges are the same.</returns>
    [return: MarshalAs(UnmanagedType.Bool)]
    bool Compare(ITextRangeProvider range);

    /// <summary>Compares one end of the range with one end of another.</summary>
    /// <param name="endpoint">This range's end.</param>
    /// <param name="targetRange">The other range.</param>
    /// <param name="targetEndpoint">The other range's end.</param>
    /// <param name="order">Receives below zero when this end is first, zero when they are the same, above zero when this end is later.</param>
    void CompareEndpoints(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint, out int order);

    /// <summary>Makes the range the unit that holds its start.</summary>
    /// <param name="unit">The unit.</param>
    void ExpandToEnclosingUnit(TextRangeUnit unit);

    /// <summary>Finds a stretch with a formatting attribute.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <param name="value">The value to look for.</param>
    /// <param name="backward">Whether to search backwards.</param>
    /// <returns>The stretch, or <see langword="null"/>.</returns>
    ITextRangeProvider? FindAttribute(int attribute, ComVariant value, [MarshalAs(UnmanagedType.Bool)] bool backward);

    /// <summary>Finds some text in the range.</summary>
    /// <param name="text">The text to look for.</param>
    /// <param name="backward">Whether to search backwards.</param>
    /// <param name="ignoreCase">Whether to ignore case.</param>
    /// <returns>The stretch, or <see langword="null"/>.</returns>
    ITextRangeProvider? FindText([MarshalAs(UnmanagedType.BStr)] string text, [MarshalAs(UnmanagedType.Bool)] bool backward, [MarshalAs(UnmanagedType.Bool)] bool ignoreCase);

    /// <summary>Gets the value of a formatting attribute over the range.</summary>
    /// <param name="attribute">The attribute.</param>
    /// <returns>The value.</returns>
    ComVariant GetAttributeValue(int attribute);

    /// <summary>Gets the rectangles that cover the range on the screen.</summary>
    /// <returns>A <c>SAFEARRAY</c> of floats, four for each rectangle.</returns>
    nint GetBoundingRectangles();

    /// <summary>Gets the element that holds the range.</summary>
    /// <returns>The element.</returns>
    IRawElementProviderSimple? GetEnclosingElement();

    /// <summary>Gets the text of the range.</summary>
    /// <param name="maxLength">The most characters to return, or -1 for all.</param>
    /// <returns>The text.</returns>
    [return: MarshalAs(UnmanagedType.BStr)]
    string GetText(int maxLength);

    /// <summary>Moves the range by whole units.</summary>
    /// <param name="unit">The unit.</param>
    /// <param name="count">How many units; negative moves back.</param>
    /// <param name="moved">Receives how many units it moved.</param>
    void Move(TextRangeUnit unit, int count, out int moved);

    /// <summary>Moves one end of the range by whole units.</summary>
    /// <param name="endpoint">The end.</param>
    /// <param name="unit">The unit.</param>
    /// <param name="count">How many units; negative moves back.</param>
    /// <param name="moved">Receives how many units it moved.</param>
    void MoveEndpointByUnit(TextPatternRangeEndpoint endpoint, TextRangeUnit unit, int count, out int moved);

    /// <summary>Moves one end of the range to an end of another range.</summary>
    /// <param name="endpoint">This range's end.</param>
    /// <param name="targetRange">The other range.</param>
    /// <param name="targetEndpoint">The other range's end.</param>
    void MoveEndpointByRange(TextPatternRangeEndpoint endpoint, ITextRangeProvider targetRange, TextPatternRangeEndpoint targetEndpoint);

    /// <summary>Selects the range.</summary>
    void Select();

    /// <summary>Adds the range to the selection.</summary>
    void AddToSelection();

    /// <summary>Removes the range from the selection.</summary>
    void RemoveFromSelection();

    /// <summary>Scrolls the range into view.</summary>
    /// <param name="alignToTop">Whether the range goes at the top of the view.</param>
    void ScrollIntoView([MarshalAs(UnmanagedType.Bool)] bool alignToTop);

    /// <summary>Gets the child elements inside the range.</summary>
    /// <returns>A <c>SAFEARRAY</c> of elements, or null when there are none.</returns>
    nint GetChildren();
}
