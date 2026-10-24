// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Where the next piece of content goes: the page, the text area, how far down it is filled, and the room footnotes have taken.</summary>
[DebuggerDisplay("FlowCursor: page {Page.Number}, y {Y}")]
internal sealed class FlowCursor
{
    /// <summary>The height in points of the separator above a page's footnotes, with its gap.</summary>
    internal const float NoteSeparatorHeight = 10F;

    /// <summary>How close to the top of the text area the cursor must be to count as being at the top.</summary>
    private const float TopTolerance = 0.01F;

    /// <summary>The pager, or <see langword="null"/> for a cursor that never runs out of room.</summary>
    private readonly IPager? _pager;

    /// <summary>Initializes a new instance of the <see cref="FlowCursor"/> class.</summary>
    /// <param name="env">The document and its styles.</param>
    /// <param name="page">The page the content goes on.</param>
    /// <param name="geometry">The text area of the page.</param>
    /// <param name="pager">Starts new pages, or <see langword="null"/> for a cursor that never runs out of room.</param>
    internal FlowCursor(LayoutEnv env, PagePlan page, PageGeometry geometry, IPager? pager)
    {
        Env = env;
        Page = page;
        _pager = pager;
        Reset(geometry);
    }

    /// <summary>Gets the document and its styles.</summary>
    internal LayoutEnv Env { get; }

    /// <summary>Gets or sets the page the content goes on.</summary>
    internal PagePlan Page { get; set; }

    /// <summary>Gets or sets the kind of artifact that content without a structure node is drawn as.</summary>
    internal PdfArtifactKind Artifact { get; set; } = PdfArtifactKind.Layout;

    /// <summary>Gets or sets the x of the left edge of the text area.</summary>
    internal float Left { get; set; }

    /// <summary>Gets or sets the width of the text area.</summary>
    internal float Width { get; set; }

    /// <summary>Gets or sets the y of the top of the text area.</summary>
    internal float Top { get; set; }

    /// <summary>Gets or sets the y of the bottom of the text area, above the footnotes.</summary>
    internal float BottomBase { get; set; }

    /// <summary>Gets or sets the y of the top of the free space.</summary>
    internal float Y { get; set; }

    /// <summary>Gets or sets the height the footnotes of the page take, with their separator.</summary>
    internal float NotesHeight { get; set; }

    /// <summary>Gets or sets the space below the last item, which collapses with the space above the next.</summary>
    internal float PendingSpace { get; set; }

    /// <summary>Gets the y of the lowest the content may reach, above the footnotes.</summary>
    internal float Bottom => BottomBase + NotesHeight;

    /// <summary>Gets a value indicating whether the cursor is at the top of the text area, where nothing has been placed.</summary>
    internal bool AtTop => Y >= Top - TopTolerance;

    /// <summary>Gets the height left on the page.</summary>
    internal float Remaining => Y - Bottom;

    /// <summary>Gets a value indicating whether the cursor can start new pages.</summary>
    internal bool CanPage => _pager is not null;

    /// <summary>Moves the cursor to the top of a text area.</summary>
    /// <param name="geometry">The text area.</param>
    internal void Reset(PageGeometry geometry)
    {
        Left = geometry.Left;
        Width = geometry.Width;
        Top = geometry.Top;
        BottomBase = geometry.Bottom;
        Y = geometry.Top;
        NotesHeight = 0F;
        PendingSpace = 0F;
    }

    /// <summary>Starts a new page.</summary>
    /// <exception cref="InvalidOperationException">The cursor cannot start pages.</exception>
    internal void NewPage()
    {
        var pager = _pager ?? throw new InvalidOperationException("Content that cannot break across pages ran out of room.");
        pager.StartPage(this);
    }

    /// <summary>Leaves the space between the last item and the next one, unless the next one starts the page.</summary>
    /// <param name="before">The space the next item wants above it.</param>
    internal void ApplySpace(float before)
    {
        if (!AtTop)
        {
            Y -= MathF.Max(PendingSpace, before);
        }

        PendingSpace = 0F;
    }

    /// <summary>Works out how much room footnotes would take on this page, beyond what is already reserved.</summary>
    /// <param name="notes">The notes to add.</param>
    /// <returns>The extra height in points.</returns>
    internal float GetNotesCost(List<LaidOutNote> notes)
    {
        if (notes.Count == 0)
        {
            return 0F;
        }

        var cost = Page.Notes.Count == 0 ? NoteSeparatorHeight : 0F;
        foreach (var note in notes)
        {
            cost += note.Height;
        }

        return cost;
    }

    /// <summary>Reserves room for footnotes on this page.</summary>
    /// <param name="notes">The notes to add, whose numbers are on the page.</param>
    internal void AddNotes(List<LaidOutNote> notes)
    {
        NotesHeight += GetNotesCost(notes);
        Page.Notes.AddRange(notes);
    }
}
