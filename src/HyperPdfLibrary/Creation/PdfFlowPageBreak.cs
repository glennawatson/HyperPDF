// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>A break that starts the next block on a new page.</summary>
[DebuggerDisplay("PdfFlowPageBreak")]
public sealed record PdfFlowPageBreak : PdfFlowBlock;
