// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Input;

namespace HyperPdf.App.Controls;

/// <summary>Names Avalonia's keys and modifiers the way the view models do.</summary>
internal static class ViewerKeyMixins
{
    /// <summary>Adds the view model names to a key press.</summary>
    /// <param name="e">The key press.</param>
    extension(KeyEventArgs e)
    {
        /// <summary>Gets the key as the view models name it.</summary>
        internal ViewerKey ViewerKey => ToViewerKey(e.Key);

        /// <summary>Gets the modifiers held, as the view models name them.</summary>
        internal ViewerModifiers ViewerModifiers => ToViewerModifiers(e.KeyModifiers);
    }

    /// <summary>Names a key as the view models do.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The key, or <see cref="ViewerKey.Other"/> when the document view does not use it.</returns>
    internal static ViewerKey ToViewerKey(Key key) => key switch
    {
        Key.Left => ViewerKey.Left,
        Key.Right => ViewerKey.Right,
        Key.Up => ViewerKey.Up,
        Key.Down => ViewerKey.Down,
        Key.PageUp => ViewerKey.PageUp,
        Key.PageDown => ViewerKey.PageDown,
        Key.Home => ViewerKey.Home,
        Key.End => ViewerKey.End,
        Key.Enter => ViewerKey.Enter,
        Key.Escape => ViewerKey.Escape,
        Key.Delete => ViewerKey.Delete,
        Key.Back => ViewerKey.Back,
        Key.Tab => ViewerKey.Tab,
        Key.Space => ViewerKey.Space,
        Key.F2 => ViewerKey.F2,
        Key.OemPlus or Key.Add => ViewerKey.Plus,
        Key.OemMinus or Key.Subtract => ViewerKey.Minus,
        _ => ViewerKey.Other,
    };

    /// <summary>Names modifier keys as the view models do.</summary>
    /// <param name="modifiers">The modifiers.</param>
    /// <returns>The same modifiers.</returns>
    internal static ViewerModifiers ToViewerModifiers(KeyModifiers modifiers)
    {
        var result = ViewerModifiers.None;
        if ((modifiers & KeyModifiers.Shift) != 0)
        {
            result |= ViewerModifiers.Shift;
        }

        if ((modifiers & KeyModifiers.Control) != 0)
        {
            result |= ViewerModifiers.Control;
        }

        if ((modifiers & KeyModifiers.Alt) != 0)
        {
            result |= ViewerModifiers.Alt;
        }

        if ((modifiers & KeyModifiers.Meta) != 0)
        {
            result |= ViewerModifiers.Meta;
        }

        return result;
    }
}
