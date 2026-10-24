// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>A character the network read in a line, and the steps of the output it came from.</summary>
/// <param name="Id">The character's number in the model's <see cref="CharacterSet"/>.</param>
/// <param name="FirstStep">The first output step the character was seen in.</param>
/// <param name="LastStep">The last output step the character was seen in.</param>
/// <param name="Confidence">How sure the network was, from 0 to 1.</param>
[DebuggerDisplay("RecognizedCharacter: #{Id} steps {FirstStep}-{LastStep} confidence {Confidence}")]
public readonly record struct RecognizedCharacter(int Id, int FirstStep, int LastStep, float Confidence);
