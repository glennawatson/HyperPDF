// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>The kinds of colorant a page paints.</summary>
public enum PdfColorantKind
{
    /// <summary>A process colorant: Cyan, Magenta, Yellow or Black.</summary>
    Process = 0,

    /// <summary>A named colorant of a Separation or DeviceN space, such as a spot colour.</summary>
    Spot = 1,

    /// <summary>The /All colorant, which marks every colorant of the device at once.</summary>
    All = 2,
}
