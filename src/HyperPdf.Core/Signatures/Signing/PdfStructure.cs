// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>A PDF file's bytes and the object locations read from its cross-reference sections.</summary>
/// <param name="Buffer">The array holding the file from its start, which may be longer than the file, such as a pooled buffer.</param>
/// <param name="Length">The file's length.</param>
/// <param name="Entries">Where each object lives, newest definition only.</param>
/// <param name="Trailer">The newest trailer dictionary, as its own bytes.</param>
/// <param name="StartXref">The offset of the newest cross-reference section.</param>
[DebuggerDisplay("PdfStructure: {Entries.Count} objects")]
internal sealed record PdfStructure(byte[] Buffer, int Length, Dictionary<int, XrefEntry> Entries, byte[] Trailer, long StartXref)
{
    /// <summary>Gets the file's bytes. Kept as an array and a length, not a memory, so the structure stays as small as it was.</summary>
    internal ReadOnlyMemory<byte> File => Buffer.AsMemory(0, Length);

    /// <summary>Gets the decoded object streams read so far, by object number.</summary>
    internal Dictionary<int, byte[]> ObjectStreams { get; } = [];

    /// <summary>Gets a value indicating whether the cross-reference information was rebuilt by scanning a damaged file.</summary>
    internal bool Repaired { get; init; }
}
