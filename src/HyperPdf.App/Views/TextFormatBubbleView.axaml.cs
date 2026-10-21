// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>A small row of the formats people reach for while typing, shown beside the text being typed.</summary>
[DebuggerDisplay("TextFormatBubbleView: {ViewModel}")]
public sealed partial class TextFormatBubbleView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TextFormatBubbleView"/> class.</summary>
    public TextFormatBubbleView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsBold, static v => v.BoldToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsItalic, static v => v.ItalicToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsUnderline, static v => v.UnderlineToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.ShrinkTextCommand, static v => v.SmallerTextButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.GrowTextCommand, static v => v.LargerTextButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.FinishTextCommand, static v => v.FinishTextButton));
        });
    }
}
