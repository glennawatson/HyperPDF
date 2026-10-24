// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Counts the footnotes of a document in reading order.</summary>
[DebuggerDisplay("NoteNumbers: {_last}")]
internal sealed class NoteNumbers
{
    /// <summary>The number given out last.</summary>
    private int _last;

    /// <summary>Gives out the next footnote number, counted from 1.</summary>
    /// <returns>The number.</returns>
    internal int Next() => ++_last;
}
