// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Text;

/// <summary>A run of characters on a text page.</summary>
/// <param name="Start">The index of the first character.</param>
/// <param name="Count">The number of characters.</param>
[DebuggerDisplay("PdfTextRange: [{Start}+{Count}]")]
public readonly record struct PdfTextRange(int Start, int Count);
