// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Text;

/// <summary>What makes one drawn piece of text different from another.</summary>
/// <param name="Text">The text.</param>
/// <param name="Style">How it looks.</param>
internal readonly record struct TextRunKey(string Text, TextStyle Style);
