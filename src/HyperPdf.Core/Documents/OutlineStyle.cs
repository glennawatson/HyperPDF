// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>How a document asks for an outline entry's title to look (the entry's <c>/C</c> and <c>/F</c>).</summary>
/// <param name="Color">The title colour as 0xRRGGBB.</param>
/// <param name="Bold">Whether the title is bold.</param>
/// <param name="Italic">Whether the title is italic.</param>
[DebuggerDisplay("OutlineStyle: {Color:X6} bold {Bold} italic {Italic}")]
public readonly record struct OutlineStyle(uint Color, bool Bold, bool Italic);
