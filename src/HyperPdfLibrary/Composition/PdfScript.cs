// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Composition;

/// <summary>Whether text sits on the line, above it or below it.</summary>
public enum PdfScript
{
    /// <summary>Text sits on the line at its own size.</summary>
    Normal = 0,

    /// <summary>Text is smaller and raised, as in x squared.</summary>
    Superscript = 1,

    /// <summary>Text is smaller and lowered, as in H two O.</summary>
    Subscript = 2,
}
