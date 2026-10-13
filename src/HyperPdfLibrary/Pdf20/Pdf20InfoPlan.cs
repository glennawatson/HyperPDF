// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>What moving the document information into XMP will do: the XMP changes, and what to tell the user.</summary>
[DebuggerDisplay("Pdf20InfoPlan: {Changes.Count} XMP changes")]
internal sealed class Pdf20InfoPlan
{
    /// <summary>Initializes a new instance of the <see cref="Pdf20InfoPlan"/> class.</summary>
    /// <param name="info">The copy of the information dictionary the plan removes entries from.</param>
    internal Pdf20InfoPlan(PdfDictionary info) => Info = info;

    /// <summary>Gets the copy of the information dictionary; entries are removed as they are handled.</summary>
    internal PdfDictionary Info { get; }

    /// <summary>Gets the changes to make to the XMP packet.</summary>
    internal List<XmpChange> Changes { get; } = [];

    /// <summary>Gets the entries added to the XMP, named in plain words.</summary>
    internal List<string> Moved { get; } = [];

    /// <summary>Gets the entries the XMP already held with the same value.</summary>
    internal List<string> Matched { get; } = [];

    /// <summary>Gets the sentences for values left out, because the XMP value wins or the value cannot be stored.</summary>
    internal List<string> Left { get; } = [];

    /// <summary>Gets or sets a value indicating whether an entry cannot be moved, so the conversion is refused.</summary>
    internal bool Blocked { get; set; }

    /// <summary>Gets or sets a value indicating whether the XMP packet was created for the moved values.</summary>
    internal bool CreatedXmp { get; set; }

    /// <summary>Gets or sets how many custom entries were added to the XMP.</summary>
    internal int CustomMoved { get; set; }

    /// <summary>Gets or sets how many custom entries the XMP already held with the same value.</summary>
    internal int CustomMatched { get; set; }

    /// <summary>Gets the dates added to the XMP.</summary>
    internal List<string> DatesAdded { get; } = [];
}
