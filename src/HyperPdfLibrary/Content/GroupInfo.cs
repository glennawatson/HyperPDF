// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Content;

/// <summary>A transparency group the interpreter asks a device to composite as one object.</summary>
/// <param name="Isolated">Whether the group is isolated from its backdrop.</param>
/// <param name="Knockout">Whether objects in the group knock out earlier ones.</param>
/// <param name="Alpha">The constant alpha the group is composited with.</param>
/// <param name="Blend">The blend mode the group is composited with.</param>
/// <param name="SoftMask">The soft mask applied to the group, or null.</param>
/// <param name="Bounds">The area the group can paint, in page space.</param>
/// <param name="BlendSpace">
/// The group's blending colour space (ISO 32000-2 11.4.5 /CS, inherited from the parent group or page when the group
/// names none), or null when no space is known and the device's own applies.
/// </param>
[DebuggerDisplay("GroupInfo: alpha {Alpha} {Blend}")]
public readonly record struct GroupInfo(bool Isolated, bool Knockout, float Alpha, PdfBlendMode Blend, PdfSoftMask? SoftMask, PdfRect Bounds, PdfColorSpace? BlendSpace)
{
    /// <summary>Initializes a new instance of the <see cref="GroupInfo"/> struct with no known blending colour space.</summary>
    /// <param name="isolated">Whether the group is isolated from its backdrop.</param>
    /// <param name="knockout">Whether objects in the group knock out earlier ones.</param>
    /// <param name="alpha">The constant alpha the group is composited with.</param>
    /// <param name="blend">The blend mode the group is composited with.</param>
    /// <param name="softMask">The soft mask applied to the group, or null.</param>
    /// <param name="bounds">The area the group can paint, in page space.</param>
    public GroupInfo(bool isolated, bool knockout, float alpha, PdfBlendMode blend, PdfSoftMask? softMask, PdfRect bounds)
        : this(isolated, knockout, alpha, blend, softMask, bounds, null)
    {
    }
}
