// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Adds parsed objects and snapshots their resources and marked content.</summary>
internal static class PageContentParseObjects
{
    /// <summary>Finds a named resource.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "category">The resource category, such as /Font.</param>
    /// <param name = "name">The resource name.</param>
    /// <returns>The value, resolved; null when missing.</returns>
    internal static PdfValue FindResource(PageContentParseState state, KnownName category, PdfName name) => state.Resources?.GetDictionary(category)?.Get(name) ?? default;

    /// <summary>Gets the open marked-content sequences as an array.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <returns>The marks, outermost first.</returns>
    internal static PdfMark[] CurrentMarks(PageContentParseState state) => state.MarkSnapshot ??= [.. state.Marks];

    /// <summary>Records an object with the state it was painted in.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "item">The object.</param>
    /// <param name = "bounds">The object's bounds in user space.</param>
    /// <param name = "source">The bytes it came from.</param>
    internal static void Add(PageContentParseState state, PdfPageObject item, in PdfRectangle bounds, ByteRange source)
    {
        item.Owner = state.Owner;
        item.Source = source;
        item.Matrix = state.State.Ctm;
        item.OriginalBounds = bounds;
        item.ClipChain = state.State.Clip;
        item.ClipBounds = state.State.Clip?.Bounds ?? PdfPageObject.Unbounded;
        item.Marks = PageContentParseObjects.CurrentMarks(state);
        item.LineWidth = state.State.LineWidth;
        item.OriginalFillPaint = state.State.Fill;
        item.OriginalStrokePaint = state.State.Stroke;
        item.OriginalFillAlpha = state.State.FillAlpha;
        item.OriginalStrokeAlpha = state.State.StrokeAlpha;
        item.OriginalBlendMode = state.State.BlendMode;
        item.Index = state.Objects.Count;
        state.Objects.Add(item);
    }
}
