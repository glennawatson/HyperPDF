// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Pdf20;

/// <summary>What a PDF 2.0 conversion may drop to get past the two refusals that need the person's say-so.</summary>
/// <param name="RemoveSignatures">Whether to remove the signatures, so a signed file is converted as an unsigned one.</param>
/// <param name="RemoveEncryption">Whether to write the copy with no protection, so a protected file the person may change is converted. Needs the owner's rights.</param>
[DebuggerDisplay("Pdf20ConversionOptions: signatures {RemoveSignatures}, protection {RemoveEncryption}")]
public readonly record struct Pdf20ConversionOptions(bool RemoveSignatures, bool RemoveEncryption)
{
    /// <summary>Gets the options that drop nothing: signed and protected files are refused.</summary>
    public static Pdf20ConversionOptions Default => default;
}
