// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Text;

/// <summary>The text, look and place of a text box, read back so it can be edited again.</summary>
/// <param name="Text">The text; line breaks start new lines.</param>
/// <param name="Format">How the text looks.</param>
/// <param name="Bounds">The box in page space (points, top-left origin).</param>
/// <param name="WrapWidth">The width lines wrap at, in points, or 0 when lines only break where the text does.</param>
[DebuggerDisplay("TextBoxContent: {Text}")]
public sealed record TextBoxContent(string Text, TextFormat Format, PageRect Bounds, float WrapWidth);
