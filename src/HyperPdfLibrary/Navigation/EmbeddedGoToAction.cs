// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Goes to a place in a PDF embedded in this one, or in the PDF this one is embedded in. Reading it opens no file.</summary>
/// <param name="EmbeddedFile">The first target step's name in the <c>/EmbeddedFiles</c> tree, when given.</param>
[DebuggerDisplay("EmbeddedGoToAction: {EmbeddedFile}")]
public sealed record EmbeddedGoToAction(string? EmbeddedFile)
{
    /// <summary>Gets the root document of the target relative to this document's root (<c>/F</c>), or null when both share a root.</summary>
    public string? File { get; init; }

    /// <summary>Gets the steps from this document to the target (<c>/T</c> and the target dictionaries nested in it), first step first.</summary>
    public PdfEmbeddedTarget[] Targets { get; init; } = [];

    /// <summary>
    /// Gets a value indicating whether the target steps refer back to an earlier step, or nest more than 64 deep. ISO
    /// 32000-2 12.6.4.4 says a viewer should refuse to carry out an action whose target path loops.
    /// </summary>
    public bool HasTargetLoop { get; init; }

    /// <summary>Gets the explicit destination in the target (<c>/D</c> as an array whose page is a page number), or null.</summary>
    public PdfDestination? Destination { get; init; }

    /// <summary>Gets the named destination in the target (<c>/D</c> as a name or string), or null.</summary>
    public string? NamedDestination { get; init; }

    /// <summary>Gets whether the target opens in a new window (<c>/NewWindow</c>), or null when the file leaves it to the viewer.</summary>
    public bool? NewWindow { get; init; }
}
