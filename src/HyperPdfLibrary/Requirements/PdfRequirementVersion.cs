// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Requirements;

/// <summary>The <c>/V</c> entry of a requirement dictionary: the least version of the feature that meets it (ISO 32000-2 §12.11.4).</summary>
/// <param name="Text">
/// The version as written, such as <c>2.0</c>, or the prefix of the one entry of an extensions dictionary. Empty when the
/// entry holds neither a name nor a dictionary.
/// </param>
/// <param name="IsExtension">Whether the entry is an extensions dictionary naming a vendor level rather than a plain version.</param>
/// <param name="IsValid">Whether the entry is a name of two or more decimal numbers, or an extensions dictionary.</param>
[DebuggerDisplay("PdfRequirementVersion: {Text} extension {IsExtension} valid {IsValid}")]
public sealed record PdfRequirementVersion(string Text, bool IsExtension, bool IsValid);
