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
public sealed record ConfirmRequest(string Title, string Message, string ConfirmText)
{
    /// <summary>Initializes a new instance of the <see cref="ConfirmRequest"/> class with retained resource identities.</summary>
    /// <param name="title">The short question.</param>
    /// <param name="message">What will happen.</param>
    /// <param name="confirmText">The confirmation button message.</param>
    public ConfirmRequest(LocalizedMessage title, LocalizedMessage message, LocalizedMessage confirmText)
        : this(title.ToString(), message.ToString(), confirmText.ToString())
    {
        TitleMessage = title;
        ContentMessage = message;
        ConfirmMessage = confirmText;
    }

    /// <summary>Gets the retained title resource, when the caller supplied one.</summary>
    public LocalizedMessage? TitleMessage { get; }

    /// <summary>Gets the retained explanation resource, when the caller supplied one.</summary>
    public LocalizedMessage? ContentMessage { get; }

    /// <summary>Gets the retained button resource, when the caller supplied one.</summary>
    public LocalizedMessage? ConfirmMessage { get; }
}
