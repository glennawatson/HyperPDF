// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A run of text with its own character formatting.</summary>
/// <param name="Text">The text. A run does not contain tabs or line breaks; use <see cref="PdfTab"/> and <see cref="PdfLineBreak"/>.</param>
[DebuggerDisplay("PdfRun: {Text}")]
public sealed record PdfRun(string Text) : PdfInline
{
    /// <summary>Gets the name of a character style to lay over the paragraph's formatting, or <see langword="null"/> for none.</summary>
    public string? Style { get; init; }

    /// <summary>Gets formatting laid over the style, or <see langword="null"/> for none.</summary>
    public PdfCharacterFormat? Format { get; init; }

    /// <summary>Makes a run in bold.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The run.</returns>
    public static PdfRun Bold(string text) => new(text) { Format = new() { Bold = true } };

    /// <summary>Makes a run in italics.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The run.</returns>
    public static PdfRun Italic(string text) => new(text) { Format = new() { Italic = true } };

    /// <summary>Makes an underlined run.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The run.</returns>
    public static PdfRun Underlined(string text) => new(text) { Format = new() { Underline = true } };

    /// <summary>Makes a run that is struck through.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The run.</returns>
    public static PdfRun Struck(string text) => new(text) { Format = new() { Strikeout = true } };

    /// <summary>Makes a raised run, such as the 2 in x squared.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The run.</returns>
    public static PdfRun Superscript(string text) => new(text) { Format = new() { Script = PdfScript.Superscript } };

    /// <summary>Makes a lowered run, such as the 2 in H two O.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The run.</returns>
    public static PdfRun Subscript(string text) => new(text) { Format = new() { Script = PdfScript.Subscript } };

    /// <summary>Makes a run in small capitals.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The run.</returns>
    public static PdfRun SmallCapitals(string text) => new(text) { Format = new() { SmallCaps = true } };
}
