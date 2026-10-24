// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Ocr.Lstm;

/// <summary>The values every layer of a model file starts with.</summary>
/// <param name="Training">Whether the layer was stored while still being trained.</param>
/// <param name="Flags">The layer's flags.</param>
/// <param name="Inputs">The number of features it takes in each step.</param>
/// <param name="Outputs">The number of features it gives in each step.</param>
/// <param name="Name">The layer's name.</param>
internal readonly record struct LayerHeader(sbyte Training, int Flags, int Inputs, int Outputs, string Name);
