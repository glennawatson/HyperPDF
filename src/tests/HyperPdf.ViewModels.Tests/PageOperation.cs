// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Tests;

/// <summary>The page-management mutation scenarios.</summary>
public enum PageOperation
{
    /// <summary>Rotate selected pages clockwise.</summary>
    RotateClockwise = 0,

    /// <summary>Rotate selected pages counterclockwise.</summary>
    RotateCounterclockwise = 1,

    /// <summary>Duplicate selected pages.</summary>
    Duplicate = 2,

    /// <summary>Delete selected pages.</summary>
    Delete = 3,

    /// <summary>Move selected pages one position earlier.</summary>
    MoveEarlier = 4,

    /// <summary>Move selected pages one position later.</summary>
    MoveLater = 5,

    /// <summary>Insert pages before the selected pages.</summary>
    Insert = 6,

    /// <summary>Append pages from another PDF.</summary>
    Merge = 7,
}
