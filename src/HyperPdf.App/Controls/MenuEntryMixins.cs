// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace HyperPdf.App.Controls;

/// <summary>Draws the context menu entries the view models describe as Avalonia menu items.</summary>
internal static class MenuEntryMixins
{
    /// <summary>The size of colour swatches in menus.</summary>
    private const double SwatchSize = 12;

    /// <summary>The corner radius of colour swatches.</summary>
    private const double SwatchRadius = 2;

    /// <summary>The alpha byte of an opaque colour, in the top byte of an ARGB value.</summary>
    private const uint OpaqueAlpha = 0xFF000000U;

    /// <summary>Adds the Avalonia drawing of a list of menu entries.</summary>
    /// <param name="entries">The entries.</param>
    extension(IReadOnlyList<MenuEntry> entries)
    {
        /// <summary>Makes the Avalonia controls that show the entries.</summary>
        /// <returns>The controls, in order.</returns>
        internal List<Control> ToControls()
        {
            var controls = new List<Control>(entries.Count);
            foreach (var entry in entries)
            {
                controls.Add(entry.ToControl());
            }

            return controls;
        }
    }

    /// <summary>Adds the Avalonia drawing of a menu entry.</summary>
    /// <param name="entry">The entry.</param>
    extension(MenuEntry entry)
    {
        /// <summary>Makes the Avalonia control that shows the entry.</summary>
        /// <returns>The control.</returns>
        internal Control ToControl() => entry.Kind switch
        {
            MenuEntryKind.Separator => new Separator(),
            MenuEntryKind.Submenu => new MenuItem { Header = entry.Header, IsEnabled = entry.IsEnabled, ItemsSource = entry.Children.ToControls() },
            _ => entry.ToItem(),
        };

        /// <summary>Makes the menu item that runs the entry's command.</summary>
        /// <returns>The item.</returns>
        internal MenuItem ToItem()
        {
            var item = new MenuItem
            {
                Header = entry.Header,
                Command = entry.Command,
                CommandParameter = entry.Parameter,
                IsEnabled = entry.IsEnabled,
                InputGesture = entry.Gesture is null ? null : KeyGesture.Parse(entry.Gesture),
            };
            if (entry.Kind == MenuEntryKind.RadioItem)
            {
                item.ToggleType = MenuItemToggleType.Radio;
                item.IsChecked = entry.IsChecked;
            }

            if (entry.Swatch is { } colour)
            {
                item.Icon = new Border { Width = SwatchSize, Height = SwatchSize, CornerRadius = new(SwatchRadius), Background = new SolidColorBrush(Color.FromUInt32(OpaqueAlpha | colour)) };
            }

            return item;
        }
    }
}
