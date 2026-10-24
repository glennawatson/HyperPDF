// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Room on a page that a floating frame takes from the text beside it.</summary>
/// <param name="Top">The y of the top of the frame.</param>
/// <param name="Bottom">The y of the bottom of the frame, with the gap below it.</param>
/// <param name="Left">The width taken from the left edge of the text area, with the gap beside the frame.</param>
/// <param name="Right">The width taken from the right edge of the text area, with the gap beside the frame.</param>
[DebuggerDisplay("Exclusion: {Top}..{Bottom}, left {Left}, right {Right}")]
internal readonly record struct Exclusion(float Top, float Bottom, float Left, float Right);
