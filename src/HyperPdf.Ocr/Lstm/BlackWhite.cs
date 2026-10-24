// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Ocr.Lstm;

/// <summary>The grey levels a line of text is stretched between.</summary>
/// <param name="Black">The level treated as ink.</param>
/// <param name="White">The level treated as paper.</param>
internal readonly record struct BlackWhite(float Black, float White);
