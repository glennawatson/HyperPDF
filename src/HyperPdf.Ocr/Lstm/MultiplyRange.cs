// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Ocr.Lstm;

/// <summary>Which steps and which of a layer's inputs a multiplication covers.</summary>
/// <param name="Steps">The number of steps multiplied.</param>
/// <param name="FirstInput">The first input whose weights are used.</param>
/// <param name="InputCount">The number of inputs multiplied.</param>
/// <param name="Accumulate">Whether the products are added to what the outputs hold rather than replacing it.</param>
[DebuggerDisplay("MultiplyRange: {Steps} steps, inputs {FirstInput} to {FirstInput + InputCount}, accumulate={Accumulate}")]
public readonly record struct MultiplyRange(int Steps, int FirstInput, int InputCount, bool Accumulate);
