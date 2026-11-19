// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Documents;

/// <summary>
/// Lists the tags that tell assistive technology how a document reads, reports what is missing, and edits them. A tag is
/// named by the <see cref="DocumentTag.Id"/> that <see cref="GetTags"/> reports. Each edit is one step in the document's
/// undo history, and a refusal comes back as a result with the reason.
/// </summary>
public interface ITagEditor : IDocumentFeature
{
    /// <summary>Gets a value indicating whether the document has tags.</summary>
    bool IsTagged { get; }

    /// <summary>Gets the tags in reading order; empty when the document is not tagged.</summary>
    /// <returns>The tags.</returns>
    List<DocumentTag> GetTags();

    /// <summary>Gets what a person should look at, such as a picture with no description.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The problems; empty when the check found none.</returns>
    List<DocumentTagProblem> GetProblems(CancellationToken cancellationToken);

    /// <summary>Gets the types a tag may take, which depend on the file's version.</summary>
    /// <param name="id">The tag.</param>
    /// <returns>The type names; empty when the tag is not in the document.</returns>
    string[] GetTypeChoices(int id);

    /// <summary>Changes a tag's type.</summary>
    /// <param name="id">The tag.</param>
    /// <param name="type">The new type, one of <see cref="GetTypeChoices"/>.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> SetType(int id, string type);

    /// <summary>Sets or clears one of a tag's texts.</summary>
    /// <param name="id">The tag.</param>
    /// <param name="field">The text.</param>
    /// <param name="text">The new text; <see langword="null"/> or empty clears it.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> SetText(int id, DocumentTagField field, string? text);

    /// <summary>Sets or clears which cells a table header cell labels.</summary>
    /// <param name="id">The tag, which must be a table header cell.</param>
    /// <param name="scope">The scope; <see cref="DocumentTableScope.None"/> clears it.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> SetTableScope(int id, DocumentTableScope scope);

    /// <summary>Moves a tag among its siblings, which changes the reading order but not the page.</summary>
    /// <param name="id">The tag.</param>
    /// <param name="offset">How many places to move it; negative moves it earlier.</param>
    /// <returns>A cold signal giving the outcome; a refusal when the tag is already at that end once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> Move(int id, int offset);

    /// <summary>Makes a tag the last child of the tag above it in its group.</summary>
    /// <param name="id">The tag.</param>
    /// <returns>A cold signal giving the outcome; a refusal when there is no tag above once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> Nest(int id);

    /// <summary>Gets the custom tag types the document maps to standard types.</summary>
    /// <returns>The mappings; empty when the document has none or is not tagged.</returns>
    DocumentRoleMapping[] GetRoleMap();

    /// <summary>Gets the standard tag types a custom type can be mapped to.</summary>
    /// <returns>The type names for the document's version.</returns>
    string[] GetStandardTypes();

    /// <summary>Maps a custom tag type to a standard type, replacing an earlier mapping for it.</summary>
    /// <param name="type">The custom type.</param>
    /// <param name="mapsTo">The standard type it stands for.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> SetRoleMapping(string type, string mapsTo);

    /// <summary>Removes a custom tag type's mapping, when no tag uses the type.</summary>
    /// <param name="type">The custom type.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> RemoveRoleMapping(string type);

    /// <summary>Sets how many rows and columns a table cell covers.</summary>
    /// <param name="id">The tag, which must be a table cell.</param>
    /// <param name="rows">How many rows, at least 1.</param>
    /// <param name="columns">How many columns, at least 1.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> SetCellSpans(int id, int rows, int columns);

    /// <summary>Sets or clears how a list's items are labelled.</summary>
    /// <param name="id">The tag, which must be a list.</param>
    /// <param name="numbering">A name such as <c>Decimal</c> or <c>Disc</c>; <see langword="null"/> or empty clears it.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> SetListNumbering(int id, string? numbering);

    /// <summary>Sets or clears how a tag sits in the flow of content.</summary>
    /// <param name="id">The tag.</param>
    /// <param name="placement">One of <c>Block</c>, <c>Inline</c>, <c>Before</c>, <c>Start</c> or <c>End</c>; <see langword="null"/> or empty clears it.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> SetPlacement(int id, string? placement);

    /// <summary>Makes a tag the last child of any other tag, which moves it to a different branch.</summary>
    /// <param name="id">The tag to move.</param>
    /// <param name="newParentId">The tag that takes it in; it cannot be inside the tag being moved.</param>
    /// <returns>A cold signal giving the outcome; a refusal when the move would break the tree once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> MoveInto(int id, int newParentId);

    /// <summary>Moves a tag out of its parent, to just after the parent.</summary>
    /// <param name="id">The tag.</param>
    /// <returns>A cold signal giving the outcome; a refusal when the tag is already at the top once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> Unnest(int id);

    /// <summary>Removes a tag that holds no page content, and lifts the tags inside it into its parent.</summary>
    /// <param name="id">The tag.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> Remove(int id);
}
