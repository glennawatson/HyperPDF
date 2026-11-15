// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>The font and size to set existing text in.</summary>
/// <param name="Family">The family of font.</param>
/// <param name="Bold">Whether the text is bold.</param>
/// <param name="Italic">Whether the text is italic.</param>
/// <param name="FontSize">The size in points, or zero or less to keep the size the text has.</param>
[DebuggerDisplay("PageTextStyle: {Family} bold {Bold} italic {Italic} size {FontSize}")]
public readonly record struct PageTextStyle(PageTextFamily Family, bool Bold, bool Italic, float FontSize);
