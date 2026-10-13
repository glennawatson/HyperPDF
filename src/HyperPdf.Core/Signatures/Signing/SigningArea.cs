// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>Where a visible signature is drawn on its page.</summary>
/// <param name="Left">The left edge in points from the page's lower left corner.</param>
/// <param name="Bottom">The bottom edge in points from the page's lower left corner.</param>
/// <param name="Right">The right edge in points.</param>
/// <param name="Top">The top edge in points.</param>
[DebuggerDisplay("SigningArea: {Left},{Bottom} to {Right},{Top}")]
public readonly record struct SigningArea(double Left, double Bottom, double Right, double Top)
{
    /// <summary>Gets a value indicating whether the area encloses nothing, which makes the signature invisible.</summary>
    public bool IsEmpty => Right <= Left || Top <= Bottom;
}
