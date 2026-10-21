// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Dialogs;

/// <summary>A question asked before a destructive action. Cancel is always the safe default.</summary>
/// <param name="Title">The short question, for example "Close 5 tabs?".</param>
/// <param name="Message">What will happen and how to undo it.</param>
/// <param name="ConfirmText">The text of the button that goes ahead.</param>
[DebuggerDisplay("ConfirmRequest: {Title}")]
public sealed record ConfirmRequest(string Title, string Message, string ConfirmText);
