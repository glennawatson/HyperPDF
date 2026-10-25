// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ReactiveUI;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Dialogs;

/// <summary>The prompt dialog: shows a <see cref="TextPrompt"/>, edits its text and answers it.</summary>
[DebuggerDisplay("PromptViewModel: {Title}")]
public sealed partial class PromptViewModel : ReactiveObject, ILocalisationChildren
{
    /// <summary>The prompt and its retained interface resources.</summary>
    private readonly TextPrompt _prompt;

    /// <summary>Initializes a new instance of the <see cref="PromptViewModel"/> class.</summary>
    /// <param name="prompt">The request.</param>
    public PromptViewModel(TextPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        _prompt = prompt;
        Multiline = prompt.Multiline;
        Text = prompt.Text;
        Answered = Signal.Merge(AcceptCommand, CancelCommand);
    }

    /// <summary>Gets the window title.</summary>
    public string Title => _prompt.TitleMessage?.ToString() ?? _prompt.Title;

    /// <summary>Gets what to type.</summary>
    public string Label => _prompt.LabelMessage?.ToString() ?? _prompt.Label;

    /// <summary>Gets the text of the button that accepts.</summary>
    public string AcceptText => _prompt.AcceptMessage?.ToString() ?? _prompt.AcceptText;

    /// <summary>Gets a value indicating whether line breaks are allowed.</summary>
    public bool Multiline { get; }

    /// <summary>Gets or sets the text being edited.</summary>
    [Reactive]
    public partial string Text { get; set; } = string.Empty;

    /// <summary>Gets the answer: the text, or <see langword="null"/> when cancelled.</summary>
    public IObservable<string?> Answered { get; }

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Title));
        this.RaisePropertyChanged(nameof(Label));
        this.RaisePropertyChanged(nameof(AcceptText));
    }

    /// <summary>Gives up without an answer.</summary>
    /// <returns>Always <see langword="null"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string? Cancel() => null;

    /// <summary>Accepts the text.</summary>
    /// <returns>The text.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private string? Accept() => Text;
}
