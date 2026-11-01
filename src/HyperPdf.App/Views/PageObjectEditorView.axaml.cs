// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.App.Controls;
using HyperPdf.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The editor that sits over the picked page text in Edit mode, so the words are retyped where they are on the page.</summary>
[DebuggerDisplay("PageObjectEditorView: {ViewModel}")]
public sealed partial class PageObjectEditorView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>The share of the text's box height the editor's font takes, as a line is taller than its letters.</summary>
    private const double FontShare = 0.8;

    /// <summary>The narrowest editor, in device independent pixels, so the caret always shows.</summary>
    private const double MinEditorWidth = 24;

    /// <summary>The degrees in a quarter turn.</summary>
    private const double QuarterTurn = 90;

    /// <summary>The degrees in a half turn.</summary>
    private const double HalfTurn = 180;

    /// <summary>The degrees in three quarter turns.</summary>
    private const double ThreeQuarterTurns = 270;

    /// <summary>Initializes a new instance of the <see cref="PageObjectEditorView"/> class.</summary>
    public PageObjectEditorView()
    {
        InitializeComponent();
        var tabs = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.Bind(ViewModel, static vm => vm.ContentEdit.EditingText, static v => v.ObjectTextEditor.Text, static text => text, static text => text ?? string.Empty));
            var edits = this.WhenChanged(static v => v.ViewModel!.ContentEdit.TextEdit);
            disposables.Add(edits.SubscribeSafe(ShowEditor, ErrorTrace.Write));

            // The editor takes the keyboard once it is placed and the press that started the edit has finished.
            disposables.Add(edits
                .KeepNotNull()
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SelectWith(ObjectTextEditor, static (editor, _) => editor)
                .FocusAtEnd());
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Zoom, static v => v.ViewModel!.Rotation, static (zoom, _) => zoom)
                .AsVoid
                .Merge(Pages!.WhenChanged(static c => c.PageScale).AsVoid)
                .SubscribeSafe(_ => Place(), ErrorTrace.Write));
            disposables.Add(ObjectTextEditor.KeyDowns.Inject(tabs).SubscribeSafe(HandleKey, ErrorTrace.Write));
            disposables.Add(ObjectTextEditor.LostFocuses
                .Where(static _ => !FocusAnnouncementRepair.IsRepeating)
                .InjectLatest(tabs)
                .SubscribeSafe(static tab => tab.ContentEdit.CommitText(), ErrorTrace.Write));
        });
    }

    /// <summary>Gets or sets the pages the editor sits over, which place and scale it.</summary>
    internal PageCanvas? Pages { get; set; }

    /// <summary>Keeps the typed words on Enter and drops them on Escape.</summary>
    /// <param name="press">The key press and the tab being edited.</param>
    private static void HandleKey(Injected<KeyEventArgs, DocumentTabViewModel> press)
    {
        var (e, tab) = press;
        switch (e.Key)
        {
            case Key.Enter:
            {
                tab.ContentEdit.CommitText();
                e.Handled = true;
                break;
            }

            case Key.Escape:
            {
                tab.ContentEdit.CancelText();
                e.Handled = true;
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Gets the turn that keeps the editor's text running along a rotated page.</summary>
    /// <param name="rotation">The page rotation.</param>
    /// <returns>The transform, or <see langword="null"/> for an upright page.</returns>
    private static RotateTransform? TurnFor(PageRotation rotation) => rotation switch
    {
        PageRotation.Rotate90 => new(QuarterTurn),
        PageRotation.Rotate180 => new(HalfTurn),
        PageRotation.Rotate270 => new(ThreeQuarterTurns),
        _ => null,
    };

    /// <summary>Shows the editor over the text being retyped, or hides it.</summary>
    /// <param name="edit">The text being retyped, or <see langword="null"/>.</param>
    private void ShowEditor(PageObjectPick? edit)
    {
        if (edit is null)
        {
            var hadFocus = ObjectTextEditor.IsKeyboardFocusWithin;
            ObjectTextEditor.IsVisible = false;
            if (hadFocus)
            {
                _ = Pages?.Focus();
            }

            return;
        }

        ObjectTextEditor.IsVisible = true;
        Place();
    }

    /// <summary>Puts the editor over the text's box at the current zoom and rotation.</summary>
    private void Place()
    {
        if (ViewModel is not { ContentEdit.TextEdit: { } edit } tab || Pages is not { } pages)
        {
            return;
        }

        var scale = pages.PageScale;
        var bounds = edit.Object.Bounds;
        var editor = ObjectTextEditor;
        var anchor = pages.PageToCanvas(edit.Page, new(bounds.Left, bounds.Top));
        editor.FontSize = Math.Max(1, bounds.Height * scale * FontShare);
        editor.Foreground = ColourBrushes.Opaque(0);
        editor.Background = PaperBrush.Get(tab.PageTone);
        editor.MinWidth = Math.Max(MinEditorWidth, bounds.Width * scale);
        editor.Width = double.NaN;
        Canvas.SetLeft(editor, anchor.X);
        Canvas.SetTop(editor, anchor.Y);
        editor.RenderTransform = TurnFor(tab.Rotation);
    }
}
