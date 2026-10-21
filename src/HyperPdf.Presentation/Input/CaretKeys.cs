// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Presentation.Input;

/// <summary>Which keys move the text caret, and where each takes it.</summary>
public static class CaretKeys
{
    /// <summary>Determines whether a key moves the caret.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> for the four arrow keys.</returns>
    public static bool IsCaretKey(ViewerKey key) => key is ViewerKey.Left or ViewerKey.Right or ViewerKey.Up or ViewerKey.Down;

    /// <summary>Works out where an arrow key moves the caret.</summary>
    /// <param name="document">The document.</param>
    /// <param name="from">The current position.</param>
    /// <param name="key">The key.</param>
    /// <param name="scratch">A list the caller owns, which receives character bounds while this works.</param>
    /// <returns>The new position, or the position on page -1 at either end of the document or for any other key.</returns>
    public static CharacterPosition GetNext(IDocument document, CharacterPosition from, ViewerKey key, List<PageRect> scratch) => key switch
    {
        ViewerKey.Right => CaretNavigation.Step(document, from, 1),
        ViewerKey.Left => CaretNavigation.Step(document, from, -1),
        ViewerKey.Down => CaretNavigation.StepLine(document, from, 1, scratch),
        ViewerKey.Up => CaretNavigation.StepLine(document, from, -1, scratch),
        _ => CaretNavigation.None(),
    };
}
