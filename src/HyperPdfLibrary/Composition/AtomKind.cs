// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>The kinds of piece a paragraph is cut into for laying out lines.</summary>
internal enum AtomKind
{
    /// <summary>Text that is not broken across lines, unless it is wider than a line.</summary>
    Word = 0,

    /// <summary>A space, where a line may break.</summary>
    Space = 1,

    /// <summary>A tab, which jumps to the next tab stop.</summary>
    Tab = 2,

    /// <summary>A forced line break.</summary>
    Break = 3,

    /// <summary>A footnote's number in the text.</summary>
    Note = 4,

    /// <summary>A place that takes no room, such as a bookmark.</summary>
    Mark = 5,
}
