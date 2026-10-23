// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Accessibility;

/// <summary>
/// One element as a screen reader sees it. A node is a snapshot: it never changes, and a change is a new node with the same
/// <paramref name="Id"/>. A heading level, a place in a set and the page text are on the node, so every platform bridge reads them
/// from the same place instead of working them out again.
/// </summary>
/// <param name="Id">The element's number, stable while the element lives.</param>
/// <param name="ParentId">The parent's number, or -1 for the application.</param>
/// <param name="Role">What the element is.</param>
/// <param name="Name">The element's name, which a screen reader speaks first.</param>
/// <param name="Description">The longer explanation, or empty.</param>
/// <param name="States">What is true of the element.</param>
/// <param name="Bounds">Where the element is on the screen.</param>
/// <param name="Children">The numbers of the children, in order.</param>
/// <param name="HeadingLevel">The level of a heading from 1, or 0 when the element is not a heading.</param>
/// <param name="PositionInSet">The place among its siblings of the same kind from 1, or 0 when it has none.</param>
/// <param name="SetSize">How many siblings of the same kind there are, or 0 when it is not part of a set.</param>
/// <param name="LandmarkType">The kind of landmark such as "main" or "navigation", or empty.</param>
/// <param name="Actions">The names of the things the element can do, such as "click"; the first is the default.</param>
/// <param name="Text">The element's text when a screen reader can read it by character, word and line, or <see langword="null"/>.</param>
/// <param name="WindowNumber">The operating system's number for a window element, such as an HWND, or 0 for any other element. A bridge that
/// answers for a whole window finds the window's element by it.</param>
[DebuggerDisplay("AccessibleNode: {Id} {Role} {Name}")]
public sealed record AccessibleNode(
    int Id,
    int ParentId,
    AccessibleRole Role,
    string Name,
    string Description,
    AccessibleStates States,
    AccessibleBounds Bounds,
    IReadOnlyList<int> Children,
    int HeadingLevel,
    int PositionInSet,
    int SetSize,
    string LandmarkType,
    IReadOnlyList<string> Actions,
    AccessibleText? Text,
    long WindowNumber);
