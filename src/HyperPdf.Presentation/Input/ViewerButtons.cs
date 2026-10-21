// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Presentation.Input;

/// <summary>The pointer buttons held down, named the same whichever toolkit reports them.</summary>
[Flags]
public enum ViewerButtons
{
    /// <summary>No button is held.</summary>
    None = 0,

    /// <summary>The left or primary button is held.</summary>
    Left = 1 << 0,

    /// <summary>The middle button or wheel is held.</summary>
    Middle = 1 << 1,

    /// <summary>The right or secondary button is held.</summary>
    Right = 1 << 2,
}
