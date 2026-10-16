// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts;

/// <summary>A family of the faces bundled with the library.</summary>
internal enum BundledFamily
{
    /// <summary>The sans serif family that stands in for Helvetica and Arial.</summary>
    Sans = 0,

    /// <summary>The serif family that stands in for Times.</summary>
    Serif = 1,

    /// <summary>The fixed-pitch family that stands in for Courier.</summary>
    Fixed = 2,

    /// <summary>The Symbol face.</summary>
    Symbol = 3,

    /// <summary>The ZapfDingbats face.</summary>
    Dingbats = 4,

    /// <summary>The monochrome emoji face.</summary>
    Emoji = 5,
}
