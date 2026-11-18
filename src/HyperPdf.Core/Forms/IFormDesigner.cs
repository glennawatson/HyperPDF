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
    /// <returns>A cold signal giving the fields, in form order once; unsubscribing cancels the work.</returns>
    IObservable<FormFieldInfo[]> GetFields();

    /// <summary>Adds a field with its widgets on the chosen pages, drawing the appearances.</summary>
    /// <param name="design">The field.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> CreateField(FormFieldDesign design);

    /// <summary>Removes a field, the fields below it and all their widgets, leaving no reference to them.</summary>
    /// <param name="name">The fully qualified name.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> RemoveField(string name);

    /// <summary>Gives a field a new fully qualified name, refusing a name that is in use.</summary>
    /// <param name="name">The current fully qualified name.</param>
    /// <param name="newName">The new fully qualified name.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> RenameField(string name, string newName);

    /// <summary>Changes the last part of a field's name, keeping the parts above it.</summary>
    /// <param name="name">The current fully qualified name.</param>
    /// <param name="newPartialName">The new last part.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> RenameFieldPartial(string name, string newPartialName);

    /// <summary>Changes the attributes of a field and draws its widgets again.</summary>
    /// <param name="name">The fully qualified name.</param>
    /// <param name="changes">The changes.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> ChangeField(string name, FormFieldChanges changes);

    /// <summary>Moves or resizes one widget of a field.</summary>
    /// <param name="name">The fully qualified name.</param>
    /// <param name="widgetIndex">The widget's position in the field, as <see cref="FormFieldInfo.Widgets"/> lists it.</param>
    /// <param name="bounds">The new rectangle in page space.</param>
    /// <returns>A cold signal giving the outcome once; unsubscribing cancels the work.</returns>
    IObservable<DocumentEditResult> MoveWidget(string name, int widgetIndex, PageRect bounds);
}
