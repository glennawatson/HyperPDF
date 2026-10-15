// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A stretch of rich text in one style; its characters are a range of the <see cref="RichText"/> they were read into.</summary>
/// <param name="Start">The first character's place in the text.</param>
/// <param name="Length">The number of characters.</param>
/// <param name="Style">The style.</param>
[DebuggerDisplay("RichRun: {Start}+{Length}")]
internal readonly record struct RichRun(int Start, int Length, RichStyle Style);
