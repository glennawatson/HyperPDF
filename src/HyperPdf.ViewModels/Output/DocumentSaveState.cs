// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Output;

/// <summary>How far one document save got, so a save that stopped part way keeps the document's edits unsaved.</summary>
internal sealed class DocumentSaveState
{
    /// <summary>Gets or sets a value indicating whether the editor wrote a complete snapshot.</summary>
    internal bool Serialized { get; set; }

    /// <summary>Gets or sets a value indicating whether the destination was replaced.</summary>
    internal bool Published { get; set; }
}
