// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>One entry of a CFF DICT: its operands followed by its operator, as a range of the DICT's bytes.</summary>
/// <param name="Operator">The operator; a two-byte operator is <c>(12 &lt;&lt; 8) | second byte</c>.</param>
/// <param name="Start">The offset of the first operand.</param>
/// <param name="End">The offset just after the operator.</param>
[DebuggerDisplay("CffDictEntry: operator {Operator}")]
internal readonly record struct CffDictEntry(int Operator, int Start, int End);
