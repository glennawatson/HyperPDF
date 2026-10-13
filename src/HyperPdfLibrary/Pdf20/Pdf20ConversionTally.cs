// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Pdf20;

/// <summary>Counts what the dictionary pass of a PDF 2.0 conversion removed, moved or found it cannot keep.</summary>
[DebuggerDisplay("Pdf20ConversionTally")]
internal sealed class Pdf20ConversionTally
{
    /// <summary>Initializes a new instance of the <see cref="Pdf20ConversionTally"/> class.</summary>
    /// <param name="names">The names the pass looks for.</param>
    internal Pdf20ConversionTally(Pdf20ConversionNames names) => Names = names;

    /// <summary>Gets the names the pass looks for.</summary>
    internal Pdf20ConversionNames Names { get; }

    /// <summary>Gets or sets how many font and XObject names were removed (ISO 32000-2 9.6.2.1, 8.9.5.1, 8.10.2).</summary>
    internal int ResourceNames { get; set; }

    /// <summary>Gets or sets how many /CharSet and /CIDSet entries were removed (9.8.1, 9.8.3.1).</summary>
    internal int GlyphSets { get; set; }

    /// <summary>Gets or sets how many procedure set lists were removed (14.2).</summary>
    internal int ProcSets { get; set; }

    /// <summary>Gets or sets how many blend mode arrays and /Compatible modes were replaced (8.4.5, 11.3.5).</summary>
    internal int BlendModes { get; set; }

    /// <summary>Gets or sets how many identity transfer functions were removed (8.4.5, 10.5).</summary>
    internal int TransferFunctions { get; set; }

    /// <summary>Gets or sets how many file specifications had operating system file names moved or removed (7.11.3, 7.11.4.1).</summary>
    internal int FileNames { get; set; }

    /// <summary>Gets or sets how many launch actions had platform launch parameters removed (12.6.4.6).</summary>
    internal int LaunchParameters { get; set; }

    /// <summary>Gets or sets how many print field attributes had their check state spelled in the current form (14.8.5.6).</summary>
    internal int PrintFields { get; set; }

    /// <summary>Gets or sets how many Web Capture identifiers were removed (14.10).</summary>
    internal int WebCaptureIds { get; set; }

    /// <summary>Gets or sets how many movie annotations were found (12.5.6.17).</summary>
    internal int MovieAnnotations { get; set; }

    /// <summary>Gets or sets how many sound annotations were found (12.5.6.16).</summary>
    internal int SoundAnnotations { get; set; }

    /// <summary>Gets or sets how many trap network annotations were found (12.5.6.21).</summary>
    internal int TrapNetworks { get; set; }

    /// <summary>Gets or sets how many movie and sound actions were found (12.6.4.9, 12.6.4.10).</summary>
    internal int MediaActions { get; set; }

    /// <summary>Gets or sets how many OPI dictionaries were found (8.9.5.1, 8.10.2, 14.11.7).</summary>
    internal int OpiDictionaries { get; set; }

    /// <summary>Gets or sets how many pages are separations of a preseparated page (14.11.4).</summary>
    internal int Separations { get; set; }

    /// <summary>Gets or sets how many transfer functions that change colour were found (8.4.5, 10.5).</summary>
    internal int ColourTransfers { get; set; }

    /// <summary>Gets or sets how many character collection descriptions name Adobe-Korea1 or Adobe-Japan2, which are kept as they are (9.7.5.2).</summary>
    internal int LegacyCollections { get; set; }

    /// <summary>Gets or sets how many launch actions name no file once the platform parameters go.</summary>
    internal int LaunchWithoutFile { get; set; }
}
