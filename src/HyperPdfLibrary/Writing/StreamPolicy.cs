// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Writing;

/// <summary>What a whole-file save does with stream data and how hard it compresses.</summary>
/// <param name="Mode">What happens to the data of each stream.</param>
/// <param name="Level">The Flate level.</param>
internal readonly record struct StreamPolicy(PdfStreamMode Mode, PdfCompressionLevel Level)
{
    /// <summary>Gets the policy the writer has always used: compress plain streams at the balanced level.</summary>
    internal static StreamPolicy Default => new(PdfStreamMode.Compress, PdfCompressionLevel.Balanced);

    /// <summary>Gets the policy an options value asks for.</summary>
    /// <param name="options">The options.</param>
    /// <returns>The policy.</returns>
    internal static StreamPolicy Of(in PdfCompactOptions options) => new(options.StreamMode, options.CompressionLevel);
}
