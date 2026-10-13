// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>How far a line of rich text rises above and falls below its baseline, and how tall its row is.</summary>
/// <param name="Ascent">The distance above the baseline.</param>
/// <param name="Descent">The distance below the baseline, a negative number.</param>
/// <param name="Height">The height of the row.</param>
[DebuggerDisplay("RichExtent: {Ascent} {Descent} {Height}")]
internal readonly record struct RichExtent(float Ascent, float Descent, float Height);
