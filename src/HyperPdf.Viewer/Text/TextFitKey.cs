// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Text;

/// <summary>What makes one piece of text cut short to fit a width different from another.</summary>
/// <param name="Text">The whole text.</param>
/// <param name="Style">How it looks.</param>
/// <param name="Width">The width it must fit, in device pixels.</param>
internal readonly record struct TextFitKey(string Text, TextStyle Style, int Width);
