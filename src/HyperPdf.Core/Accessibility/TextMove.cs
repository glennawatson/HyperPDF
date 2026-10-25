// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Accessibility;

/// <summary>The outcome of moving a text range.</summary>
/// <param name="Range">Where the range is after the move.</param>
/// <param name="Moved">How many units it moved; negative when it moved back, and less than asked for when it ran into an end of the text.</param>
[DebuggerDisplay("TextMove: {Range} moved {Moved}")]
public readonly record struct TextMove(TextSpan Range, int Moved);
