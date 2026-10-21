// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Media;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>Asks for some text, for example a note; closes with the text, or <see langword="null"/> when cancelled.</summary>
[DebuggerDisplay("PromptWindow: {Title}")]
public sealed partial class PromptWindow : ReactiveUI.Avalonia.ReactiveWindow<PromptViewModel>
{
    /// <summary>The height of a multi-line text box.</summary>
    private const double MultilineHeight = 96;

    /// <summary>The height of a single line text box.</summary>
    private const double SingleLineHeight = 32;

    /// <summary>Initializes a new instance of the <see cref="PromptWindow"/> class.</summary>
    public PromptWindow()
    {
        InitializeComponent();

        // The box has no name of its own: it is named by the question above it, which changes with each prompt.
        FieldLabels.Link((InputBox, LabelText));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.Title));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Label, static v => v.LabelText.Text));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Text, static v => v.InputBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Multiline, static v => v.InputBox.AcceptsReturn));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Multiline, static v => v.InputBox.TextWrapping, static multiline => multiline ? TextWrapping.Wrap : TextWrapping.NoWrap));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Multiline, static v => v.InputBox.MinHeight, static multiline => multiline ? MultilineHeight : SingleLineHeight));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.AcceptText, static v => v.ConfirmButton.Content));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Multiline, static v => v.ConfirmButton.IsDefault, static multiline => !multiline));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.AcceptCommand, static v => v.ConfirmButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Answered)
                .SwitchMap(static answered => answered)
                .SubscribeSafe(Close, ErrorTrace.Write));

            _ = InputBox.Focus();
            InputBox.SelectAll();
        });
    }
}
