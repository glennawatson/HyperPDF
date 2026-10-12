// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>One cross-reference section of a file and the trailer entries the structure rules read, as written.</summary>
/// <param name="Offset">Where the section starts.</param>
/// <param name="IsStream">Whether the section is a cross-reference stream.</param>
/// <param name="Previous">The /Prev offset, or <see langword="null"/>.</param>
/// <param name="Hybrid">The /XRefStm offset, or <see langword="null"/>.</param>
/// <param name="Identifier">The joined bytes of the /ID strings, or <see langword="null"/> when /ID is not an array.</param>
internal sealed record PdfAXrefSection(long Offset, bool IsStream, long? Previous, long? Hybrid, byte[]? Identifier);
