// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A stretch of rich text in one style. A line feed in the text is a line break.</summary>
/// <param name="Text">The text.</param>
/// <param name="Style">The style.</param>
[DebuggerDisplay("RichRun: {Text}")]
internal sealed record RichRun(string Text, RichStyle Style);
