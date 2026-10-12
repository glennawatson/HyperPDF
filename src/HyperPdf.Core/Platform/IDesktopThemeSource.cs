// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Advanced;

namespace HyperPdf.Core.Platform;

/// <summary>Supplies the desktop colour scheme.</summary>
public interface IDesktopThemeSource
{
    /// <summary>Gets the palette: the current value on subscription, then each change; null when the desktop has none.</summary>
    AsObservableSignal<DesktopPalette?> Palette { get; }
}
