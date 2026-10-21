// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Viewing;

/// <summary>The modifier keys held with a key, named the same whichever toolkit reports them.</summary>
[Flags]
public enum ViewerModifiers
{
    /// <summary>No modifier is held.</summary>
    None = 0,

    /// <summary>Shift is held.</summary>
    Shift = 1 << 0,

    /// <summary>Control is held.</summary>
    Control = 1 << 1,

    /// <summary>Alt is held.</summary>
    Alt = 1 << 2,

    /// <summary>The Windows key, Command or Meta is held.</summary>
    Meta = 1 << 3,
}
