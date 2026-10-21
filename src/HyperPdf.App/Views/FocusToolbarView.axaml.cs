// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ReactiveUI;
using ReactiveUI.Binding;

namespace HyperPdf.App.Views;

/// <summary>Focus Mode's tool bar: smaller and larger text, the text settings, and the way back to the pages.</summary>
[DebuggerDisplay("FocusToolbarView: {ViewModel}")]
public sealed partial class FocusToolbarView : ReactiveUI.Avalonia.ReactiveUserControl<FocusModeViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="FocusToolbarView"/> class.</summary>
    public FocusToolbarView()
    {
        InitializeComponent();
        ColourBox.ItemsSource = FocusModeViewModel.PageColourOptions;
        FontBox.ItemsSource = FocusModeViewModel.FontOptions;
        ColourBox.ItemTemplate = new FuncDataTemplate<string>(static (text, _) => new TextBlock { Text = text });
        FontBox.ItemTemplate = new FuncDataTemplate<string>(static (text, _) => new TextBlock { Text = text });
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.SmallerCommand, static v => v.SmallerButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.LargerCommand, static v => v.LargerButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ToggleCommand, static v => v.BackToPagesButton));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FontSize, static v => v.SizeSlider.Value));
            disposables.Add(this.Bind(ViewModel, static vm => vm.LineSpacing, static v => v.LineSlider.Value));
            disposables.Add(this.Bind(ViewModel, static vm => vm.ParagraphSpacing, static v => v.ParagraphSlider.Value));
            disposables.Add(this.Bind(ViewModel, static vm => vm.TextWidth, static v => v.WidthSlider.Value));
            disposables.Add(this.Bind(ViewModel, static vm => vm.PageColour, static v => v.ColourBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Font, static v => v.FontBox.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FocusBand, static v => v.BandCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.WordHighlight, static v => v.WordCheck.IsChecked, static on => on, static on => on == true));
        });
    }
}
