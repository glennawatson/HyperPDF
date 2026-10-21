// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Preferences;

/// <summary>Holds the settings a background tab takes when it is selected; used on the UI thread only.</summary>
internal sealed class TabSettingsSlot
{
    /// <summary>The settings that wait, or <see langword="null"/>.</summary>
    private TabSettings? _pending;

    /// <summary>Keeps settings for the tab to take when it is selected, replacing any that waited.</summary>
    /// <param name="settings">The settings.</param>
    internal void Defer(TabSettings settings) => _pending = settings;

    /// <summary>Takes the waiting settings, if any.</summary>
    /// <param name="settings">The settings taken.</param>
    /// <returns><see langword="true"/> when settings waited.</returns>
    internal bool TryTake(out TabSettings settings)
    {
        settings = _pending.GetValueOrDefault();
        var waited = _pending is not null;
        _pending = null;
        return waited;
    }
}
