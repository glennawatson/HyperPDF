// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A page edit applied as one undo step.</summary>
/// <param name="Kind">The operation.</param>
/// <param name="Pages">Zero-based page indexes, in selection order.</param>
/// <param name="Value">Rotation in degrees, or the destination index for moving or copying; ignored for deletion.</param>
[DebuggerDisplay("{Kind}: {Pages.Length} pages")]
public readonly record struct PageEditRequest(PageEditKind Kind, ReadOnlyMemory<int> Pages, int Value);
