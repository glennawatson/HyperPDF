// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>A run of consecutive code-blocks in a tile.</summary>
/// <param name="First">The index of the first code-block.</param>
/// <param name="Count">The number of code-blocks.</param>
internal readonly record struct JpxBlockRun(int First, int Count);
