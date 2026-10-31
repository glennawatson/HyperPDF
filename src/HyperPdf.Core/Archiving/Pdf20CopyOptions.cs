// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Archiving;

/// <summary>What a PDF 2.0 copy may drop to get past the two refusals that need the person's say-so.</summary>
/// <param name="RemoveSignatures">Whether to remove the signatures, so a signed file is copied as an unsigned one.</param>
/// <param name="RemoveProtection">Whether to write the copy with no protection. Only for someone who holds the owner's rights.</param>
[DebuggerDisplay("Pdf20CopyOptions: signatures {RemoveSignatures}, protection {RemoveProtection}")]
public readonly record struct Pdf20CopyOptions(bool RemoveSignatures, bool RemoveProtection);
