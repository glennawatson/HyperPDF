// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Text;

/// <summary>How a piece of window text looks.</summary>
/// <param name="Size">The text size in device pixels.</param>
/// <param name="Bold">Whether the text is bold.</param>
/// <param name="Color">The colour as ARGB.</param>
[DebuggerDisplay("TextStyle: {Size} px, bold {Bold}")]
public readonly record struct TextStyle(int Size, bool Bold, uint Color);
