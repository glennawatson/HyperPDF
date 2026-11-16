// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>The way a line of text reads on the page as it is shown, to the nearest quarter turn.</summary>
public enum TextFlow
{
    /// <summary>The line reads to the right, and the lines of its paragraph follow one another downward.</summary>
    Right = 0,

    /// <summary>The line reads upward, and the lines of its paragraph follow one another to the right.</summary>
    Up = 1,

    /// <summary>The line reads to the left, and the lines of its paragraph follow one another upward.</summary>
    Left = 2,

    /// <summary>The line reads downward, and the lines of its paragraph follow one another to the left.</summary>
    Down = 3,
}
