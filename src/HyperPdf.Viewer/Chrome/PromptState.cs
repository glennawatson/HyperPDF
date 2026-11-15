// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What a dialog that asks for one line of text shows, and whether it was answered.</summary>
/// <param name="Request">What is asked.</param>
/// <param name="Entry">The text typed so far.</param>
/// <param name="Answered">Whether the dialog was answered. Only the first answer counts.</param>
/// <param name="Answer">The text the dialog answered with, or <see langword="null"/> when it was turned down.</param>
[DebuggerDisplay("PromptState: {Request.Title}, answered {Answered}")]
internal sealed record PromptState(PromptRequest Request, string Entry, bool Answered, string? Answer);
