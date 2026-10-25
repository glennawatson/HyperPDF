// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Ocr.Lstm;

/// <summary>A code the network chose at one step.</summary>
/// <param name="Code">The code.</param>
/// <param name="Probability">The code's probability.</param>
internal readonly record struct CodeChoice(int Code, float Probability);
