// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>The state of a line being filled with atoms.</summary>
[DebuggerDisplay("LineFill: cursor {Cursor}")]
internal sealed class LineFill
{
    /// <summary>Initializes a new instance of the <see cref="LineFill"/> class.</summary>
    /// <param name="start">Where the line's content starts, from the left edge of the text area.</param>
    /// <param name="end">Where the line's content must end.</param>
    /// <param name="firstAtom">The first atom of the line.</param>
    internal LineFill(float start, float end, int firstAtom)
    {
        Start = start;
        End = end;
        Cursor = start;
        AcceptedEnd = firstAtom;
    }

    /// <summary>Gets where the line's content starts.</summary>
    internal float Start { get; }

    /// <summary>Gets where the line's content must end.</summary>
    internal float End { get; }

    /// <summary>Gets or sets where the next atom goes.</summary>
    internal float Cursor { get; set; }

    /// <summary>Gets or sets the width of the spaces seen since the last word, which only count if a word follows on the same line.</summary>
    internal float PendingWidth { get; set; }

    /// <summary>Gets or sets the number of spaces seen since the last word.</summary>
    internal int PendingSpaces { get; set; }

    /// <summary>Gets or sets the number of spaces inside the line's content.</summary>
    internal int AcceptedSpaces { get; set; }

    /// <summary>Gets or sets the place after the last atom that is part of the line.</summary>
    internal int AcceptedEnd { get; set; }

    /// <summary>Gets or sets a value indicating whether the line has an atom that takes room.</summary>
    internal bool HasContent { get; set; }

    /// <summary>Gets or sets a value indicating whether the line is full or ended.</summary>
    internal bool Done { get; set; }

    /// <summary>Gets or sets a value indicating whether the line ends because the next word did not fit.</summary>
    internal bool Wrapped { get; set; }
}
