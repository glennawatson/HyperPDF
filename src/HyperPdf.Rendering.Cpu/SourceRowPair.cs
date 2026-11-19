// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Cpu;

/// <summary>The two source rows a smooth scaled frame line lies between.</summary>
/// <param name="Above">The source row above the frame line.</param>
/// <param name="Below">The source row below it, which is the same row at the bottom edge.</param>
internal readonly record struct SourceRowPair(int Above, int Below);
