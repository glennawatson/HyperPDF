// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>The names the conversion looks for that <see cref="KnownName"/> does not list, interned once per conversion.</summary>
[DebuggerDisplay("Pdf20ConversionNames")]
internal sealed class Pdf20ConversionNames
{
    /// <summary>Initializes a new instance of the <see cref="Pdf20ConversionNames"/> class.</summary>
    /// <param name="names">The working copy's name table.</param>
    internal Pdf20ConversionNames(PdfNameTable names)
    {
        SpiderInfo = names.Intern("SpiderInfo"u8);
        SeparationInfo = names.Intern("SeparationInfo"u8);
        Dos = names.Intern("DOS"u8);
        ViewArea = names.Intern("ViewArea"u8);
        ViewClip = names.Intern("ViewClip"u8);
        PrintArea = names.Intern("PrintArea"u8);
        PrintClip = names.Intern("PrintClip"u8);
        Suspects = names.Intern("Suspects"u8);
        AlternatePresentations = names.Intern("AlternatePresentations"u8);
        Checked = names.Intern("Checked"u8);
        PrintField = names.Intern("PrintField"u8);
        MovieActions = names.Intern("MovieActions"u8);
        SoundActions = names.Intern("SoundActions"u8);
        ExternalOpiDictionaries = names.Intern("ExternalOPIdicts"u8);
        PieceInfo = names.Intern("PieceInfo"u8);
        Default = names.Intern("Default"u8);
        Ordering = names.Intern("Ordering"u8);
    }

    /// <summary>Gets <c>/SpiderInfo</c>, the catalog's Web Capture information.</summary>
    internal PdfName SpiderInfo { get; }

    /// <summary>Gets <c>/SeparationInfo</c>, a preseparated page's separation dictionary.</summary>
    internal PdfName SeparationInfo { get; }

    /// <summary>Gets <c>/DOS</c>, a file specification's DOS file name.</summary>
    internal PdfName Dos { get; }

    /// <summary>Gets <c>/ViewArea</c>.</summary>
    internal PdfName ViewArea { get; }

    /// <summary>Gets <c>/ViewClip</c>.</summary>
    internal PdfName ViewClip { get; }

    /// <summary>Gets <c>/PrintArea</c>.</summary>
    internal PdfName PrintArea { get; }

    /// <summary>Gets <c>/PrintClip</c>.</summary>
    internal PdfName PrintClip { get; }

    /// <summary>Gets <c>/Suspects</c>, the mark information's tag suspects flag.</summary>
    internal PdfName Suspects { get; }

    /// <summary>Gets <c>/AlternatePresentations</c>, the name dictionary's slide show tree.</summary>
    internal PdfName AlternatePresentations { get; }

    /// <summary>Gets <c>/Checked</c>, a print field attribute.</summary>
    internal PdfName Checked { get; }

    /// <summary>Gets <c>/PrintField</c>, the print field attribute owner.</summary>
    internal PdfName PrintField { get; }

    /// <summary>Gets <c>/MovieActions</c>, a legal attestation count.</summary>
    internal PdfName MovieActions { get; }

    /// <summary>Gets <c>/SoundActions</c>, a legal attestation count.</summary>
    internal PdfName SoundActions { get; }

    /// <summary>Gets <c>/ExternalOPIdicts</c>, a legal attestation count.</summary>
    internal PdfName ExternalOpiDictionaries { get; }

    /// <summary>Gets <c>/PieceInfo</c>, the page-piece dictionary.</summary>
    internal PdfName PieceInfo { get; }

    /// <summary>Gets the name <c>/Default</c>.</summary>
    internal PdfName Default { get; }

    /// <summary>Gets <c>/Ordering</c>, the character collection of a CIDSystemInfo dictionary.</summary>
    internal PdfName Ordering { get; }
}
