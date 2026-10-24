// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Ocr;

namespace HyperPdf.Ocr.Layout;

/// <summary>What the words of a page are reported with and where they go.</summary>
/// <param name="PixelsPerPoint">The page resolution, used to report positions in points.</param>
/// <param name="Output">Receives the words.</param>
/// <param name="Scratch">The working memory.</param>
[DebuggerDisplay("WordRequest: {PixelsPerPoint} pixels per point")]
public readonly record struct WordRequest(float PixelsPerPoint, List<OcrWord> Output, RecognitionScratch Scratch);
