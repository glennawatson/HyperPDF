// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Signatures.Signing;

/// <summary>Where an object lives: free, at a byte offset, or inside an object stream.</summary>
/// <param name="Type">0 free, 1 at <paramref name="Location"/> bytes, 2 in object stream <paramref name="Location"/>.</param>
/// <param name="Location">The byte offset or the object stream's number.</param>
/// <param name="Index">The object's index in its object stream.</param>
[DebuggerDisplay("XrefEntry: {Type}: {Location}/{Index}")]
internal readonly record struct XrefEntry(int Type, long Location, int Index)
{
    /// <summary>The type of an object stored at a byte offset.</summary>
    internal const int InUse = 1;

    /// <summary>The type of an object packed into an object stream.</summary>
    internal const int Compressed = 2;
}
