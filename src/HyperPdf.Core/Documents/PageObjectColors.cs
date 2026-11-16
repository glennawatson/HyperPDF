// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Documents;

/// <summary>A change to the colours of a page object. A colour that is not asked for is left as it is.</summary>
/// <param name="ChangeFill">Whether the fill colour changes.</param>
/// <param name="Fill">The new fill colour as 0xRRGGBB, when it changes.</param>
/// <param name="ChangeStroke">Whether the stroke colour changes.</param>
/// <param name="Stroke">The new stroke colour as 0xRRGGBB, when it changes.</param>
[DebuggerDisplay("PageObjectColors: fill {ChangeFill} {Fill:X6}, stroke {ChangeStroke} {Stroke:X6}")]
public readonly record struct PageObjectColors(bool ChangeFill, uint Fill, bool ChangeStroke, uint Stroke);
