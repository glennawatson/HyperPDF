// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// One inferred structure element of a page, in a flat list in reading order with each element followed by its
/// descendants. It owns either some units or child elements, never both.
/// </summary>
/// <param name="Tag">The structure type.</param>
/// <param name="UnitStart">Where its units start in the build's unit list.</param>
/// <param name="UnitCount">How many units it owns.</param>
/// <param name="Size">The number of elements in its subtree, itself included, so the next sibling follows after that many.</param>
/// <param name="IsFigure">Whether it is a figure, which needs alternative text.</param>
/// <param name="Language">The language guessed from the element's text, or <see langword="null"/>.</param>
/// <param name="AlternateText">The description a host gave a figure, or <see langword="null"/>.</param>
/// <param name="Cell">The table cell attributes of a header or data cell, or <see langword="null"/>.</param>
[DebuggerDisplay("TagNode: {Tag}, {UnitCount} units, {Size} in subtree")]
internal readonly record struct TagNode(PdfName Tag, int UnitStart, int UnitCount, int Size, bool IsFigure, string? Language, string? AlternateText, TagCell? Cell);
