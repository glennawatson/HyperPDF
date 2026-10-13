// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A page of a preseparated document that paints one colourant of a printed page.</summary>
/// <param name="PageIndex">The zero-based page index.</param>
/// <param name="Colorant">The colourant, for example Cyan; empty when the file does not name one.</param>
[DebuggerDisplay("SeparationPage: {PageIndex} {Colorant}")]
public readonly record struct SeparationPage(int PageIndex, string Colorant);
