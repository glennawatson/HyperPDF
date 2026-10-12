// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.App.ViewModels;

/// <summary>A request for the user to type some text, for example a note.</summary>
/// <param name="Title">The window title.</param>
/// <param name="Label">What to type.</param>
/// <param name="Text">The starting text.</param>
/// <param name="AcceptText">The text of the button that accepts.</param>
/// <param name="Multiline">Whether line breaks are allowed.</param>
[DebuggerDisplay("TextPrompt: {Title}")]
public sealed record TextPrompt(string Title, string Label, string Text, string AcceptText, bool Multiline);
