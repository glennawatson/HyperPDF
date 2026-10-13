// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Document;

/// <summary>The name trees the catalog's <c>/Names</c> dictionary can hold (ISO 32000-2 7.7.4, Table 31).</summary>
public enum PdfNameTreeKind
{
    /// <summary>Named destinations (<c>/Dests</c>).</summary>
    Destinations = 0,

    /// <summary>Embedded files (<c>/EmbeddedFiles</c>).</summary>
    EmbeddedFiles = 1,

    /// <summary>Document-level JavaScript (<c>/JavaScript</c>).</summary>
    JavaScript = 2,

    /// <summary>Appearance streams (<c>/AP</c>).</summary>
    AppearanceStreams = 3,

    /// <summary>Named pages (<c>/Pages</c>).</summary>
    Pages = 4,

    /// <summary>Named page templates (<c>/Templates</c>).</summary>
    Templates = 5,

    /// <summary>Digital identifiers (<c>/IDS</c>).</summary>
    DigitalIdentifiers = 6,

    /// <summary>URL alias dictionaries (<c>/URLS</c>).</summary>
    UrlAliases = 7,

    /// <summary>Alternate presentations (<c>/AlternatePresentations</c>).</summary>
    AlternatePresentations = 8,

    /// <summary>Renditions (<c>/Renditions</c>).</summary>
    Renditions = 9,
}
