// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Pdf20;

/// <summary>What the conversion did with one document information value.</summary>
internal enum Pdf20InfoRoute
{
    /// <summary>The value was empty, so there was nothing to move.</summary>
    Skipped = 0,

    /// <summary>The value was added to the XMP.</summary>
    Moved = 1,

    /// <summary>The XMP already held the same value.</summary>
    Matched = 2,

    /// <summary>The value was left out, because the XMP holds a different one or XMP cannot store it.</summary>
    Left = 3,
}
