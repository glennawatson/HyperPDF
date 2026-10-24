// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Charts;

/// <summary>A slice of a pie and the pie it is cut from.</summary>
/// <param name="CenterX">The x of the pie's middle.</param>
/// <param name="CenterY">The y of the pie's middle.</param>
/// <param name="Radius">The pie's radius.</param>
/// <param name="Value">The slice's value.</param>
internal readonly record struct PieSlice(float CenterX, float CenterY, float Radius, double Value);
