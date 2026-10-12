// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Optimizing;

/// <summary>What a saved optimised copy may change.</summary>
/// <param name="Preset">How much picture detail may be traded for size.</param>
/// <param name="FixAccessibility">Whether a missing language, title and tagging flags are filled in.</param>
/// <param name="Language">The language written when the document has none, such as "en-AU"; <see langword="null"/> or empty writes none.</param>
/// <param name="AddInferredTags">Whether an untagged document gets a basic structure inferred from its layout.</param>
/// <param name="CleanUp">Whether thumbnails, private application data, unused resources and empty annotation lists are removed.</param>
[DebuggerDisplay("OptimizeSettings: {Preset}, accessibility {FixAccessibility}, tags {AddInferredTags}, clean up {CleanUp}")]
public sealed record OptimizeSettings(OptimizePreset Preset, bool FixAccessibility, string? Language, bool AddInferredTags, bool CleanUp)
{
    /// <summary>Gets the settings used when the user changes nothing: balanced, with the accessibility entries filled in.</summary>
    public static OptimizeSettings Default { get; } = new(OptimizePreset.Balanced, true, null, false, false);
}
