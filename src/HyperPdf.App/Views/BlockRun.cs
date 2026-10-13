// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.Views;

/// <summary>A stretch of a Focus Mode block that is marked the same way.</summary>
/// <param name="Start">Where the run starts in the block's text.</param>
/// <param name="Length">The run's length.</param>
/// <param name="Spoken">Whether the run is inside the sentence being read.</param>
/// <param name="Word">Whether the run is inside the word being read.</param>
internal readonly record struct BlockRun(int Start, int Length, bool Spoken, bool Word);
