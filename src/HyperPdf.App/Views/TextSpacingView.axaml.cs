// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.App.Controls;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>The menu that sets the line and letter spacing of the text being typed.</summary>
[DebuggerDisplay("TextSpacingView: {ViewModel}")]
public sealed partial class TextSpacingView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TextSpacingView"/> class.</summary>
    public TextSpacingView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.SpacingName, static v => v.SpacingText.Text));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineSpacingCommand, static v => v.SingleSpacingItem, "Single".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineSpacingCommand, static v => v.NormalSpacingItem, "Normal".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineSpacingCommand, static v => v.OneHalfSpacingItem, "One and a half".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetLineSpacingCommand, static v => v.DoubleSpacingItem, "Double".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetCharacterSpacingCommand, static v => v.TightLettersItem, "Tight".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetCharacterSpacingCommand, static v => v.NormalLettersItem, "Normal".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetCharacterSpacingCommand, static v => v.WideLettersItem, "Wide".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetCharacterSpacingCommand, static v => v.WiderLettersItem, "Wider".AsCommandParameter));
        });
    }
}
