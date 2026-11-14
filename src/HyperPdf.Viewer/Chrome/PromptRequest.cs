// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What a dialog that asks for one line of text shows.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Label">What the text is for.</param>
/// <param name="Text">The text the box starts with.</param>
/// <param name="AcceptText">The text of the button that agrees.</param>
/// <param name="Masked">Whether the text is a password, drawn as dots.</param>
[DebuggerDisplay("PromptRequest: {Title}")]
public sealed record PromptRequest(string Title, string Label, string Text, string AcceptText, bool Masked);
