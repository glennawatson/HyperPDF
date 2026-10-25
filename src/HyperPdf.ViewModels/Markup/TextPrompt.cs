// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.ViewModels.Markup;

/// <summary>A request for the user to type some text, for example a note.</summary>
/// <param name="Title">The window title.</param>
/// <param name="Label">What to type.</param>
/// <param name="Text">The starting text.</param>
/// <param name="AcceptText">The text of the button that accepts.</param>
/// <param name="Multiline">Whether line breaks are allowed.</param>
[DebuggerDisplay("TextPrompt: {Title}")]
public sealed record TextPrompt(string Title, string Label, string Text, string AcceptText, bool Multiline)
{
    /// <summary>Initializes a new instance of the <see cref="TextPrompt"/> class with retained interface resource identities.</summary>
    /// <param name="title">The window title.</param>
    /// <param name="label">What to type.</param>
    /// <param name="text">The user's starting text.</param>
    /// <param name="acceptText">The confirmation button text.</param>
    /// <param name="multiline">Whether line breaks are allowed.</param>
    public TextPrompt(LocalizedMessage title, LocalizedMessage label, string text, LocalizedMessage acceptText, bool multiline)
        : this(title.ToString(), label.ToString(), text, acceptText.ToString(), multiline)
    {
        TitleMessage = title;
        LabelMessage = label;
        AcceptMessage = acceptText;
    }

    /// <summary>Gets the retained title resource, when the caller supplied one.</summary>
    public LocalizedMessage? TitleMessage { get; }

    /// <summary>Gets the retained field label resource, when the caller supplied one.</summary>
    public LocalizedMessage? LabelMessage { get; }

    /// <summary>Gets the retained button resource, when the caller supplied one.</summary>
    public LocalizedMessage? AcceptMessage { get; }
}
