// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>
/// A key and value from a block's meta data (ECMA-363 9.2.6). The reader only records it. A value the file marks as an action or a
/// file to open is kept as text and is never run or opened.
/// </summary>
/// <param name="Block">The type of the block the entry came from.</param>
/// <param name="Key">The key.</param>
/// <param name="Value">The value as text, or empty for a binary value.</param>
/// <param name="Attributes">The entry's flags: 1 binary, 2 hidden, 0x100 action, 0x200 file, 0x400 MIME data.</param>
[DebuggerDisplay("U3dMetadataEntry: {Key}")]
public sealed record U3dMetadataEntry(uint Block, string Key, string Value, uint Attributes)
{
    /// <summary>The flag for an action the viewer might run.</summary>
    private const uint ActionFlag = 0x100;

    /// <summary>The flag for a file the viewer might open.</summary>
    private const uint FileFlag = 0x200;

    /// <summary>Gets a value indicating whether the file asks for this value to be run or opened. It never is.</summary>
    public bool AsksToRun => (Attributes & (ActionFlag | FileFlag)) != 0;
}
