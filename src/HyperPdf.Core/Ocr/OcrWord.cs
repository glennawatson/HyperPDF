// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Ocr;

/// <summary>A word found by text recognition.</summary>
/// <param name="Text">The word.</param>
/// <param name="Bounds">Where it is on the page, in page space (points, top-left origin).</param>
/// <param name="Confidence">How sure the recogniser is, from 0 to 100.</param>
[DebuggerDisplay("OcrWord: {Text} ({Confidence})")]
public readonly record struct OcrWord(string Text, PageRect Bounds, float Confidence);
