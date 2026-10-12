// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using HyperPdf.Core.Reading;

namespace HyperPdf.Core.Tests.Reading;

/// <summary>Builds a page's characters by placing lines of text, as a PDF engine would report them.</summary>
internal sealed class PageBuilder
{
    /// <summary>A US Letter page.</summary>
    internal static readonly PageSize Letter = new(612, 792);

    /// <summary>The body text size.</summary>
    private const float BodySize = 10;

    /// <summary>A character's width as a share of its font size.</summary>
    private const float AdvanceShare = 0.5F;

    /// <summary>Gets the characters placed so far, in the order placed.</summary>
    internal List<PageCharacter> Characters { get; } = [];

    /// <summary>Places a line of text with its top-left corner at a point, each word separated by a real space.</summary>
    /// <param name="text">The text.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <param name="size">The font size.</param>
    /// <param name="bold">Whether bold.</param>
    /// <returns>This builder.</returns>
    internal PageBuilder Line(string text, float left, float top, float size, bool bold)
    {
        var x = left;
        foreach (var c in text)
        {
            var width = size * AdvanceShare;
            Characters.Add(new(c, c == ' ' ? default : new(x, top, width, size), size, bold, false));
            x += width;
        }

        // Engines insert a line break after each line.
        Characters.Add(new('\n', default, size, bold, true));
        return this;
    }

    /// <summary>Places a line of 10 point body text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="left">The left edge.</param>
    /// <param name="top">The top edge.</param>
    /// <returns>This builder.</returns>
    internal PageBuilder Body(string text, float left, float top) => Line(text, left, top, BodySize, false);

    /// <summary>Works out the page's reading order.</summary>
    /// <param name="repeated">The repeated margin signatures.</param>
    /// <returns>The block texts in reading order.</returns>
    internal ReadingPage Read(IReadOnlySet<string>? repeated = null) => ReadingOrder.Analyze(0, Letter, Characters, repeated ?? new HashSet<string>());
}
