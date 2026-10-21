// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.ViewModels.Reading;

/// <summary>Moves a text caret through a document by character and by line, whichever toolkit draws the caret.</summary>
public static class CaretNavigation
{
    /// <summary>How far, as a share of the caret's height, each probe looks beyond the caret's line.</summary>
    private const float LineProbe = 0.75F;

    /// <summary>How many probes are made before giving up on the page.</summary>
    private const int MaxLineProbes = 12;

    /// <summary>Half, for the middle of a character.</summary>
    private const float Half = 0.5F;

    /// <summary>Gets the position that means there is no character.</summary>
    /// <returns>The position, on page -1.</returns>
    public static CharacterPosition None() => new(-1, -1);

    /// <summary>Determines whether a key moves the caret.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> for the four arrow keys.</returns>
    public static bool IsCaretKey(ViewerKey key) => key is ViewerKey.Left or ViewerKey.Right or ViewerKey.Up or ViewerKey.Down;

    /// <summary>Works out where an arrow key moves the caret.</summary>
    /// <param name="document">The document.</param>
    /// <param name="from">The current position.</param>
    /// <param name="key">The key.</param>
    /// <param name="scratch">A list the caller owns, which receives character bounds while this works.</param>
    /// <returns>The new position, or <see cref="None"/> at either end of the document or for any other key.</returns>
    public static CharacterPosition GetNext(IDocument document, CharacterPosition from, ViewerKey key, List<PageRect> scratch) => key switch
    {
        ViewerKey.Right => Step(document, from, 1),
        ViewerKey.Left => Step(document, from, -1),
        ViewerKey.Down => StepLine(document, from, 1, scratch),
        ViewerKey.Up => StepLine(document, from, -1, scratch),
        _ => None(),
    };

    /// <summary>Steps one character, crossing to the next or previous page with text.</summary>
    /// <param name="document">The document.</param>
    /// <param name="from">The current position.</param>
    /// <param name="direction">1 for forward, -1 for back.</param>
    /// <returns>The new position, or <see cref="None"/> at either end of the document.</returns>
    public static CharacterPosition Step(IDocument document, CharacterPosition from, int direction)
    {
        ArgumentNullException.ThrowIfNull(document);
        var target = from.Char + direction;
        if (target >= 0 && target < document.GetCharacterCount(from.Page))
        {
            return new(from.Page, target);
        }

        for (var page = from.Page + direction; page >= 0 && page < document.PageCount; page += direction)
        {
            var count = document.GetCharacterCount(page);
            if (count > 0)
            {
                return new(page, direction > 0 ? 0 : count - 1);
            }
        }

        return None();
    }

    /// <summary>Steps one line down or up, keeping the caret's horizontal position, crossing pages at the edges.</summary>
    /// <param name="document">The document.</param>
    /// <param name="from">The current position.</param>
    /// <param name="direction">1 for down, -1 for up.</param>
    /// <param name="scratch">A list the caller owns, which receives character bounds while this works.</param>
    /// <returns>The new position, or <see cref="None"/> at either end of the document.</returns>
    public static CharacterPosition StepLine(IDocument document, CharacterPosition from, int direction, List<PageRect> scratch)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(scratch);
        scratch.Clear();
        document.GetTextBounds(from.Page, from.Char, 1, scratch);
        if (scratch.Count > 0)
        {
            var bounds = scratch[0];
            var height = Math.Max(1, bounds.Height);
            for (var probe = 1; probe <= MaxLineProbes; probe++)
            {
                var y = direction > 0 ? bounds.Bottom + (height * LineProbe * probe) : bounds.Top - (height * LineProbe * probe);
                var found = document.GetCharacterIndexAt(from.Page, new(bounds.Left, y), height);
                if (found >= 0 && found != from.Char && IsOtherLine(document, from.Page, found, bounds, direction, scratch))
                {
                    return new(from.Page, found);
                }
            }
        }

        // No further line on this page: go to the first or last character of the next page with text.
        var edge = direction > 0 ? new CharacterPosition(from.Page, document.GetCharacterCount(from.Page) - 1) : new CharacterPosition(from.Page, 0);
        return Step(document, edge, direction);
    }

    /// <summary>Determines whether a character sits on a line beyond the caret's, not just reaching past it like a descender.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="character">The character found.</param>
    /// <param name="caret">The caret character's bounds.</param>
    /// <param name="direction">1 for down, -1 for up.</param>
    /// <param name="scratch">A list the caller owns, which receives the character's bounds.</param>
    /// <returns><see langword="true"/> when its centre is past the caret's line.</returns>
    private static bool IsOtherLine(IDocument document, int page, int character, in PageRect caret, int direction, List<PageRect> scratch)
    {
        scratch.Clear();
        document.GetTextBounds(page, character, 1, scratch);
        if (scratch.Count == 0)
        {
            return false;
        }

        var centre = scratch[0].Top + (scratch[0].Height * Half);
        return direction > 0 ? centre > caret.Bottom : centre < caret.Top;
    }
}
