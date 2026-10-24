// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>How text after a tab lines up with the tab stop.</summary>
public enum PdfTabAlignment
{
    /// <summary>The text starts at the stop.</summary>
    Left = 0,

    /// <summary>The text is centred on the stop.</summary>
    Center = 1,

    /// <summary>The text ends at the stop.</summary>
    Right = 2,

    /// <summary>The decimal point of a number is at the stop.</summary>
    Decimal = 3,
}
