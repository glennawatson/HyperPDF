// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;

namespace HyperPdf.App.Controls;

/// <summary>Form fields on the page canvas: clicking a field fills it, and the editor overlay is placed from here.</summary>
public sealed partial class PageCanvas
{
    /// <summary>The width of the field focus ring.</summary>
    private const double FocusRingWidth = 2;

    /// <summary>How far outside the field the focus ring sits.</summary>
    private const double FocusRingGap = 3;

    /// <summary>Gets the canvas units per point at the current zoom, for sizing an editor placed on a page.</summary>
    internal double PageScale => _layout.Options.Scale;

    /// <summary>Converts a rectangle on a page to canvas coordinates, for placing an editor over a field.</summary>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The rectangle in page space.</param>
    /// <returns>The canvas rectangle, or an empty one when the page is not laid out.</returns>
    public Rect GetCanvasRect(int page, PageRect bounds) => Tab is not { } tab || (uint)page >= (uint)_sizes.Length
        ? default
        : new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale).ToCanvas(bounds);

    /// <summary>Fills the field under a click, if there is one.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when a field took the click.</returns>
    private bool TryActivateField(DocumentTabViewModel tab, int page, Point position)
    {
        if (tab.Forms.HitTest(page, ToPage(tab, page, position)) is not { } field)
        {
            return false;
        }

        if (!tab.Forms.Activate(field) && field.Kind is FormFieldKind.ComboBox or FormFieldKind.ListBox)
        {
            ShowChoices(tab.Forms, field);
        }

        InvalidateVisual();
        return true;
    }

    /// <summary>Opens a menu of a choice field's options, with the current one ticked.</summary>
    /// <param name="forms">The form state.</param>
    /// <param name="field">The field.</param>
    private void ShowChoices(FormsViewModel forms, FormField field)
    {
        var items = new List<Control>(field.Options.Count);
        for (var i = 0; i < field.Options.Count; i++)
        {
            var item = Item(field.Options[i], null, forms.ChooseOptionCommand, new FormChoice(field, i), true);
            item.ToggleType = MenuItemToggleType.Radio;
            item.IsChecked = i == field.SelectedOption;
            items.Add(item);
        }

        new ContextMenu { ItemsSource = items, Placement = PlacementMode.Pointer }.Open(this);
    }

    /// <summary>
    /// Moves between form fields with Tab and Shift+Tab, ticks the focused box with Space, opens a focused list with
    /// Enter, and drops the field focus with Escape. Tab only moves between fields while filling in a form, so it still
    /// leaves the pages otherwise, and past the last field.
    /// </summary>
    /// <param name="e">The key press.</param>
    /// <returns><see langword="true"/> when a field took the key.</returns>
    private bool HandleFormKey(KeyEventArgs e)
    {
        if (Tab is not { Forms: { HasForm: true } forms } tab)
        {
            return false;
        }

        var focused = forms.Focused;
        return e.Key switch
        {
            Key.Tab when focused is not null || tab.FillAndSign.IsActive => MoveField(forms, (e.KeyModifiers & KeyModifiers.Shift) != 0),
            Key.Space when focused is not null => forms.PressFocused() && Redraw(),
            Key.Enter when focused is { Kind: FormFieldKind.ComboBox or FormFieldKind.ListBox } => OpenChoices(forms, focused),
            Key.Escape when focused is not null => forms.ClearFocus() && Redraw(),
            _ => false,
        };
    }

    /// <summary>Moves the field focus, scrolling the field into view.</summary>
    /// <param name="forms">The form state.</param>
    /// <param name="backwards">Whether to move to the previous field.</param>
    /// <returns><see langword="true"/> when a field took the focus.</returns>
    private bool MoveField(FormsViewModel forms, bool backwards)
    {
        if (forms.CommitAndMove(backwards) is not { } field)
        {
            return false;
        }

        Tab?.NavigateTo(new(field.PageIndex, field.Bounds, 0));
        return Redraw();
    }

    /// <summary>Opens the choices of a focused list.</summary>
    /// <param name="forms">The form state.</param>
    /// <param name="field">The field.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    private bool OpenChoices(FormsViewModel forms, FormField field)
    {
        ShowChoices(forms, field);
        return true;
    }

    /// <summary>Redraws the canvas after a key changed what it shows.</summary>
    /// <returns>Always <see langword="true"/>.</returns>
    private bool Redraw()
    {
        InvalidateVisual();
        return true;
    }

    /// <summary>Draws a ring around the field with the keyboard focus, so it is clear where Space and Tab act.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    private void DrawFieldFocus(DrawingContext context, DocumentTabViewModel tab)
    {
        if (tab.Forms.Focused is not { } field || tab.Forms.Editing is not null || (uint)field.PageIndex >= (uint)_sizes.Length)
        {
            return;
        }

        context.DrawRectangle(null, new Pen((_currentHitPen ?? StrokePen).Brush, FocusRingWidth), GetCanvasRect(field.PageIndex, field.Bounds).Inflate(FocusRingGap));
    }

    /// <summary>Determines whether a canvas point is over a fillable field.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when over a field.</returns>
    private bool IsOverField(Point position)
    {
        var page = _layout.HitTest(position.X, position.Y);
        return page >= 0 && Tab is { } tab && tab.Forms.HitTest(page, ToPage(tab, page, position)) is not null;
    }
}
