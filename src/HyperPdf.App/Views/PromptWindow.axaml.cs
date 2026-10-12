// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using HyperPdf.App.ViewModels;
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
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Multiline, static v => v.InputBox.TextWrapping, Wrapping));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Multiline, static v => v.InputBox.MinHeight, BoxHeight));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.AcceptText, static v => v.ConfirmButton.Content));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Multiline, static v => v.ConfirmButton.IsDefault, IsSingleLine));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.AcceptCommand, static v => v.ConfirmButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Answered)
                .SwitchMap(static answered => answered)
                .SubscribeSafe(Close, static error => Trace.TraceError(error.ToString())));

            _ = InputBox.Focus();
            InputBox.SelectAll();
        });
    }

    /// <summary>Gets the text wrapping for a text box.</summary>
    /// <param name="multiline">Whether line breaks are allowed.</param>
    /// <returns>Wrapping for multi-line boxes, none otherwise.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static TextWrapping Wrapping(bool multiline) => multiline ? TextWrapping.Wrap : TextWrapping.NoWrap;

    /// <summary>Gets the minimum height of the text box.</summary>
    /// <param name="multiline">Whether line breaks are allowed.</param>
    /// <returns>The height.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double BoxHeight(bool multiline) => multiline ? MultilineHeight : SingleLineHeight;

    /// <summary>Inverts the multi-line flag.</summary>
    /// <param name="multiline">Whether line breaks are allowed.</param>
    /// <returns><see langword="true"/> when the box is single line.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsSingleLine(bool multiline) => !multiline;
}
