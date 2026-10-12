// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>An undoable change to a document's selected pages.</summary>
public enum PageEditKind
{
    /// <summary>Removes the selected pages, keeping at least one page.</summary>
    Delete = 0,

    /// <summary>Turns each selected page by a number of degrees clockwise.</summary>
    Rotate = 1,

    /// <summary>Moves the selected pages to an index in the resulting document.</summary>
    Move = 2,

    /// <summary>Copies the selected pages before an index in the current document.</summary>
    Duplicate = 3,
}
