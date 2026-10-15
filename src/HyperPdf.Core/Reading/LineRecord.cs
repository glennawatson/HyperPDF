// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Reading;

/// <summary>A line being worked out: where its text sits in the shared buffers and what is known about its box and font.</summary>
[DebuggerDisplay("LineRecord: {Visible} characters")]
internal record struct LineRecord
{
    /// <summary>Gets or sets the position of the line's first character in the shared text buffer.</summary>
    internal int Start { get; set; }

    /// <summary>Gets or sets the characters of the line, with inferred spaces.</summary>
    internal int Count { get; set; }

    /// <summary>Gets or sets the visible characters.</summary>
    internal int Visible { get; set; }

    /// <summary>Gets or sets the line's box.</summary>
    internal PageRect Bounds { get; set; }

    /// <summary>Gets or sets the last character's box.</summary>
    internal PageRect Last { get; set; }

    /// <summary>Gets or sets the sum of the characters' font sizes.</summary>
    internal float SizeTotal { get; set; }

    /// <summary>Gets or sets the bold characters.</summary>
    internal int Bold { get; set; }

    /// <summary>Gets or sets a value indicating whether the line's first letter is from a right-to-left script.</summary>
    internal bool IsRightToLeft { get; set; }

    /// <summary>Gets or sets the line that follows this one in its block, or -1.</summary>
    internal int Next { get; set; }

    /// <summary>Gets the average font size.</summary>
    internal readonly float FontSize => Visible == 0 ? 0 : SizeTotal / Visible;

    /// <summary>Gets a value indicating whether most characters are bold.</summary>
    internal readonly bool IsBold => Bold > Visible - Bold;
}
