// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>What fills the space a tab skips.</summary>
public enum PdfTabLeader
{
    /// <summary>Nothing: the space is empty.</summary>
    None = 0,

    /// <summary>A row of dots, as in a table of contents.</summary>
    Dots = 1,

    /// <summary>A row of dashes.</summary>
    Dashes = 2,

    /// <summary>A solid line along the baseline.</summary>
    Line = 3,
}
