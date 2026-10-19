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
    IReadOnlyList<DocumentTag> GetTags();

    /// <summary>Gets what a person should look at, such as a picture with no description.</summary>
    /// <param name="cancellationToken">Cancels the check.</param>
    /// <returns>The problems; empty when the check found none.</returns>
    IReadOnlyList<DocumentTagProblem> GetProblems(CancellationToken cancellationToken);

    /// <summary>Gets the types a tag may take, which depend on the file's version.</summary>
    /// <param name="id">The tag.</param>
    /// <returns>The type names; empty when the tag is not in the document.</returns>
    IReadOnlyList<string> GetTypeChoices(int id);

    /// <summary>Changes a tag's type.</summary>
    /// <param name="id">The tag.</param>
    /// <param name="type">The new type, one of <see cref="GetTypeChoices"/>.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> SetTypeAsync(int id, string type, CancellationToken cancellationToken);

    /// <summary>Sets or clears one of a tag's texts.</summary>
    /// <param name="id">The tag.</param>
    /// <param name="field">The text.</param>
    /// <param name="text">The new text; <see langword="null"/> or empty clears it.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> SetTextAsync(int id, DocumentTagField field, string? text, CancellationToken cancellationToken);

    /// <summary>Sets or clears which cells a table header cell labels.</summary>
    /// <param name="id">The tag, which must be a table header cell.</param>
    /// <param name="scope">The scope; <see cref="DocumentTableScope.None"/> clears it.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> SetTableScopeAsync(int id, DocumentTableScope scope, CancellationToken cancellationToken);

    /// <summary>Moves a tag among its siblings, which changes the reading order but not the page.</summary>
    /// <param name="id">The tag.</param>
    /// <param name="offset">How many places to move it; negative moves it earlier.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome; a refusal when the tag is already at that end.</returns>
    ValueTask<DocumentEditResult> MoveAsync(int id, int offset, CancellationToken cancellationToken);

    /// <summary>Makes a tag the last child of the tag above it in its group.</summary>
    /// <param name="id">The tag.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome; a refusal when there is no tag above.</returns>
    ValueTask<DocumentEditResult> NestAsync(int id, CancellationToken cancellationToken);

    /// <summary>Gets the custom tag types the document maps to standard types.</summary>
    /// <returns>The mappings; empty when the document has none or is not tagged.</returns>
    IReadOnlyList<DocumentRoleMapping> GetRoleMap();

    /// <summary>Gets the standard tag types a custom type can be mapped to.</summary>
    /// <returns>The type names for the document's version.</returns>
    IReadOnlyList<string> GetStandardTypes();

    /// <summary>Maps a custom tag type to a standard type, replacing an earlier mapping for it.</summary>
    /// <param name="type">The custom type.</param>
    /// <param name="mapsTo">The standard type it stands for.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> SetRoleMappingAsync(string type, string mapsTo, CancellationToken cancellationToken);

    /// <summary>Removes a custom tag type's mapping, when no tag uses the type.</summary>
    /// <param name="type">The custom type.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> RemoveRoleMappingAsync(string type, CancellationToken cancellationToken);

    /// <summary>Sets how many rows and columns a table cell covers.</summary>
    /// <param name="id">The tag, which must be a table cell.</param>
    /// <param name="rows">How many rows, at least 1.</param>
    /// <param name="columns">How many columns, at least 1.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> SetCellSpansAsync(int id, int rows, int columns, CancellationToken cancellationToken);

    /// <summary>Sets or clears how a list's items are labelled.</summary>
    /// <param name="id">The tag, which must be a list.</param>
    /// <param name="numbering">A name such as <c>Decimal</c> or <c>Disc</c>; <see langword="null"/> or empty clears it.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> SetListNumberingAsync(int id, string? numbering, CancellationToken cancellationToken);

    /// <summary>Sets or clears how a tag sits in the flow of content.</summary>
    /// <param name="id">The tag.</param>
    /// <param name="placement">One of <c>Block</c>, <c>Inline</c>, <c>Before</c>, <c>Start</c> or <c>End</c>; <see langword="null"/> or empty clears it.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> SetPlacementAsync(int id, string? placement, CancellationToken cancellationToken);

    /// <summary>Makes a tag the last child of any other tag, which moves it to a different branch.</summary>
    /// <param name="id">The tag to move.</param>
    /// <param name="newParentId">The tag that takes it in; it cannot be inside the tag being moved.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome; a refusal when the move would break the tree.</returns>
    ValueTask<DocumentEditResult> MoveIntoAsync(int id, int newParentId, CancellationToken cancellationToken);

    /// <summary>Moves a tag out of its parent, to just after the parent.</summary>
    /// <param name="id">The tag.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome; a refusal when the tag is already at the top.</returns>
    ValueTask<DocumentEditResult> UnnestAsync(int id, CancellationToken cancellationToken);

    /// <summary>Removes a tag that holds no page content, and lifts the tags inside it into its parent.</summary>
    /// <param name="id">The tag.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> RemoveAsync(int id, CancellationToken cancellationToken);
}
