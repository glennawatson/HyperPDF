// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr.Layout;

/// <summary>What a model read from one line: the characters, and how much of the line's width an output step stands for.</summary>
/// <param name="Characters">The characters, left to right.</param>
/// <param name="PixelsPerStep">The number of pixels of the line window an output step covers.</param>
/// <param name="Confidence">The mean confidence of the characters that are not spaces, from 0 to 1.</param>
[DebuggerDisplay("LineReading: {Characters.Count} characters, confidence {Confidence}")]
public readonly record struct LineReading(List<RecognizedCharacter> Characters, double PixelsPerStep, double Confidence);
