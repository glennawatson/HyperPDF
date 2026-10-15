// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>Every setting of a text box with exact numbers; closes with whether to apply them.</summary>
[DebuggerDisplay("TextPropertiesWindow: {Title}")]
public sealed partial class TextPropertiesWindow : ReactiveUI.Avalonia.ReactiveWindow<TextPropertiesViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="TextPropertiesWindow"/> class.</summary>
    public TextPropertiesWindow()
    {
        InitializeComponent();
        FamilyInput.ItemTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock { Text = name, FontFamily = PageFonts.Get(ViewModel?.PageFontFamilies, name) });
        AlignmentInput.ItemsSource = TextPropertiesViewModel.Alignments;
        ColourInput.ItemsSource = TextPropertiesViewModel.ColorNames;
        FieldLabels.Link(
            (ContentInput, TextLabel),
            (FamilyInput, FamilyLabel),
            (SizeInput, SizeLabel),
            (ColourInput, ColourLabel),
            (AlignmentInput, AlignmentLabel),
            (WrapInput, WrapLabel),
            (LineSpacingInput, LineSpacingLabel),
            (LetterSpacingInput, LetterSpacingLabel),
            (CombInput, CombLabel));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.Bind(ViewModel, static vm => vm.Text, static v => v.ContentInput.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.FontFamilies, static v => v.FamilyInput.ItemsSource));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FontFamily, static v => v.FamilyInput.SelectedItem, static family => family, static item => item as string ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.FontSize, static v => v.SizeInput.Value, static size => size, Number));
            disposables.Add(this.Bind(ViewModel, static vm => vm.ColorIndex, static v => v.ColourInput.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsBold, static v => v.BoldInput.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsItalic, static v => v.ItalicInput.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsUnderline, static v => v.UnderlineInput.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.AlignmentIndex, static v => v.AlignmentInput.SelectedIndex));
            disposables.Add(this.Bind(ViewModel, static vm => vm.WrapWidth, static v => v.WrapInput.Value, static width => width, Number));
            disposables.Add(this.Bind(ViewModel, static vm => vm.LineSpacing, static v => v.LineSpacingInput.Value, static spacing => spacing, Number));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CharacterSpacing, static v => v.LetterSpacingInput.Value, static spacing => spacing, Number));
            disposables.Add(this.Bind(ViewModel, static vm => vm.CombCells, static v => v.CombInput.Value, static cells => cells, Number));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.AcceptCommand, static v => v.ApplyButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Answered)
                .SwitchMap(static answered => answered)
                .SubscribeSafe(apply => Close(apply), ErrorTrace.Write));
            _ = ContentInput.Focus();
        });
    }

    /// <summary>Reads a number box, treating an empty box as 0.</summary>
    /// <param name="value">The box's value.</param>
    /// <returns>The number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static decimal Number(decimal? value) => value ?? 0;
}
