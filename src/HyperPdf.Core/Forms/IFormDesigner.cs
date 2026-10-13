// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Forms;

/// <summary>
/// Adds, edits, renames and removes form fields (ISO 32000-2 12.7). Each edit is one step in the document's undo history.
/// A refusal comes back as a result with the reason in plain words, for example a name already in use, or a script in a
/// document that claims PDF/A.
/// </summary>
public interface IFormDesigner : IDocumentFeature
{
    /// <summary>Reads every field of the form with the attributes the form design edits.</summary>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>The fields, in form order.</returns>
    ValueTask<IReadOnlyList<FormFieldInfo>> GetFieldsAsync(CancellationToken cancellationToken);

    /// <summary>Adds a field with its widgets on the chosen pages, drawing the appearances.</summary>
    /// <param name="design">The field.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> CreateFieldAsync(FormFieldDesign design, CancellationToken cancellationToken);

    /// <summary>Removes a field, the fields below it and all their widgets, leaving no reference to them.</summary>
    /// <param name="name">The fully qualified name.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> RemoveFieldAsync(string name, CancellationToken cancellationToken);

    /// <summary>Gives a field a new fully qualified name, refusing a name that is in use.</summary>
    /// <param name="name">The current fully qualified name.</param>
    /// <param name="newName">The new fully qualified name.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> RenameFieldAsync(string name, string newName, CancellationToken cancellationToken);

    /// <summary>Changes the last part of a field's name, keeping the parts above it.</summary>
    /// <param name="name">The current fully qualified name.</param>
    /// <param name="newPartialName">The new last part.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> RenameFieldPartialAsync(string name, string newPartialName, CancellationToken cancellationToken);

    /// <summary>Changes the attributes of a field and draws its widgets again.</summary>
    /// <param name="name">The fully qualified name.</param>
    /// <param name="changes">The changes.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> ChangeFieldAsync(string name, FormFieldChanges changes, CancellationToken cancellationToken);

    /// <summary>Moves or resizes one widget of a field.</summary>
    /// <param name="name">The fully qualified name.</param>
    /// <param name="widgetIndex">The widget's position in the field, as <see cref="FormFieldInfo.Widgets"/> lists it.</param>
    /// <param name="bounds">The new rectangle in page space.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>The outcome.</returns>
    ValueTask<DocumentEditResult> MoveWidgetAsync(string name, int widgetIndex, PageRect bounds, CancellationToken cancellationToken);
}
