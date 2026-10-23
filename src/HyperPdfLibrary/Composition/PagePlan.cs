// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>A page as laid out: its size and every command to draw on it, waiting to be written.</summary>
[DebuggerDisplay("PagePlan: {Number}, {Commands.Count} commands")]
internal sealed class PagePlan
{
    /// <summary>Initializes a new instance of the <see cref="PagePlan"/> class.</summary>
    /// <param name="number">The page's place in the document, counted from 1.</param>
    /// <param name="section">The section the page belongs to.</param>
    /// <param name="sectionPage">The page's place in its section, counted from 1.</param>
    /// <param name="displayNumber">The number the page shows, which a section can restart.</param>
    internal PagePlan(int number, SectionInfo section, int sectionPage, int displayNumber)
    {
        Number = number;
        Section = section;
        SectionPage = sectionPage;
        DisplayNumber = displayNumber;
    }

    /// <summary>Gets the page's place in the document, counted from 1.</summary>
    internal int Number { get; }

    /// <summary>Gets the section the page belongs to.</summary>
    internal SectionInfo Section { get; }

    /// <summary>Gets the page's place in its section, counted from 1.</summary>
    internal int SectionPage { get; }

    /// <summary>Gets the number the page shows.</summary>
    internal int DisplayNumber { get; }

    /// <summary>Gets the commands to draw, in order.</summary>
    internal List<DrawCommand> Commands { get; } = [];

    /// <summary>Gets the room floating frames take from the text beside them on the page.</summary>
    internal List<Exclusion> Exclusions { get; } = [];

    /// <summary>Gets the notes whose numbers appear on the page, in order, waiting to be drawn at the foot of the page.</summary>
    internal List<LaidOutNote> Notes { get; } = [];
}
