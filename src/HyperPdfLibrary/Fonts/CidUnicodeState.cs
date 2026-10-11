// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>Stores CID-to-Unicode tables.</summary>
[DebuggerDisplay("CID Unicode tables")]
internal sealed class CidUnicodeState
{
    /// <summary>The collection slots, including no collection.</summary>
    internal const int CollectionCount = (int)CjkScript.Korean + 1;

    /// <summary>Gets the gate for reading tables.</summary>
    internal Lock Gate { get; } = new();

    /// <summary>Gets the parsed tables.</summary>
    internal CidToUnicodeTable?[] Tables { get; } = new CidToUnicodeTable?[CollectionCount];
}
