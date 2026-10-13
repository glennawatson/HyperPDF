// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>A script the shaper treats in its own way, with the OpenType script tags to look for in a font.</summary>
/// <param name="Name">The ISO 15924 code, such as <c>Arab</c>.</param>
/// <param name="Kind">How text in the script is shaped.</param>
/// <param name="Tags">The OpenType script tags to try, best first.</param>
[DebuggerDisplay("ShapingScript: {Name}")]
internal sealed record ShapingScript(string Name, ShapingKind Kind, uint[] Tags)
{
    /// <summary>Gets the script of characters that belong to no script, such as digits and punctuation; it takes the script of the text around it.</summary>
    internal static ShapingScript Common { get; } = new("Zyyy", ShapingKind.Default, [OtTags.DefaultScript]);

    /// <summary>Gets a value indicating whether the script has no script of its own.</summary>
    internal bool IsCommon => ReferenceEquals(this, Common);
}
