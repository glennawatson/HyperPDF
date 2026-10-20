// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Optimizing;

/// <summary>The kind of font object whose names get a subset tag.</summary>
internal enum FontRenameKind
{
    /// <summary>A font dictionary: its <c>/BaseFont</c> changes, and so do any descendant font and descriptor stored inside it.</summary>
    Font = 0,

    /// <summary>A descendant font of a Type 0 font: its <c>/BaseFont</c> changes, and so does any descriptor stored inside it.</summary>
    Descendant = 1,

    /// <summary>A font descriptor: its <c>/FontName</c> changes.</summary>
    Descriptor = 2,
}
