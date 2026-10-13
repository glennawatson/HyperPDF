// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>A heading. The level is kept in order: a heading is at most one level below the one before it, and the first is level 1.</summary>
/// <param name="Level">The level, 1 to 6.</param>
/// <param name="Text">The heading's text.</param>
[DebuggerDisplay("PdfFlowHeading: H{Level} {Text}")]
public sealed record PdfFlowHeading(int Level, string Text) : PdfFlowBlock;
