// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Creation;

/// <summary>A block of a document to create, laid out in order from the top of the first page.</summary>
[DebuggerDisplay("CreationBlock")]
public abstract record CreationBlock
{
    /// <summary>Initializes a new instance of the <see cref="CreationBlock"/> class; only the blocks in this library derive from it.</summary>
    private protected CreationBlock()
    {
    }
}
