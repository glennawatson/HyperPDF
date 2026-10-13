// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Forms;

namespace HyperPdf.Engine;

/// <summary>Adds, edits, renames and removes form fields of an open document through the library.</summary>
internal sealed class HyperPdfFormDesignerService : IFormDesigner
{
    /// <summary>The document owning this feature's resources.</summary>
    private readonly HyperPdfDocument _owner;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfFormDesignerService"/> class.</summary>
    /// <param name="owner">The owning document.</param>
    internal HyperPdfFormDesignerService(HyperPdfDocument owner) => _owner = owner;

    /// <inheritdoc/>
    public IDocument Document => _owner;

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<FormFieldInfo>> GetFieldsAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_owner.IsDisposed, _owner);
        await HyperPdfEditPrefetch.PagesAsync(_owner.Document, cancellationToken).ConfigureAwait(false);
        return ReadFields(cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentEditResult> CreateFieldAsync(FormFieldDesign design, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(design);
        return HyperPdfDocumentEdits.RunCheckedAsync(
            _owner,
            new DesignStep(design, cancellationToken),
            HyperPdfEditPrefetch.PagesAsync,
            static (document, step) =>
            {
                _ = PdfFormFieldCreation.CreateField(document, HyperPdfFormDesignMapping.ToDefinition(document, step.Design), step.CancellationToken);
                return null;
            },
            PdfCacheScopes.Structure,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentEditResult> RemoveFieldAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return HyperPdfDocumentEdits.RunCheckedAsync(
            _owner,
            new NameStep(name, cancellationToken),
            HyperPdfEditPrefetch.PagesAsync,
            static (document, step) =>
            {
                var removal = PdfFormFieldRemoval.RemoveField(document, step.Name, step.CancellationToken);
                if (removal.Removed.Length > 0)
                {
                    return null;
                }

                return removal.Skipped.Length > 0 ? string.Join(' ', removal.Skipped) : "That field is not in the form.";
            },
            PdfCacheScopes.Structure,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentEditResult> RenameFieldAsync(string name, string newName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(newName);
        return HyperPdfDocumentEdits.RunCheckedAsync(
            _owner,
            new RenameStep(name, newName, cancellationToken),
            HyperPdfEditPrefetch.PagesAsync,
            static (document, step) =>
            {
                var rename = PdfFormFieldRenaming.RenameField(document, step.Name, step.NewName, step.CancellationToken);
                return rename.Renamed ? null : rename.Reason ?? "The field could not be renamed.";
            },
            PdfCacheScopes.None,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentEditResult> RenameFieldPartialAsync(string name, string newPartialName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(newPartialName);
        return HyperPdfDocumentEdits.RunCheckedAsync(
            _owner,
            new RenameStep(name, newPartialName, cancellationToken),
            HyperPdfEditPrefetch.PagesAsync,
            static (document, step) =>
            {
                var rename = PdfFormFieldRenaming.RenameFieldPartial(document, step.Name, step.NewName, step.CancellationToken);
                return rename.Renamed ? null : rename.Reason ?? "The field could not be renamed.";
            },
            PdfCacheScopes.None,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentEditResult> ChangeFieldAsync(string name, FormFieldChanges changes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(changes);
        return HyperPdfDocumentEdits.RunCheckedAsync(
            _owner,
            new ChangeStep(name, changes, cancellationToken),
            HyperPdfEditPrefetch.PagesAsync,
            static (document, step) =>
            {
                var result = PdfFormFieldEditing.ChangeField(document, step.Name, HyperPdfFormDesignMapping.ToChanges(step.Changes), step.CancellationToken);
                return result.Applied ? null : result.Reason ?? "The field could not be changed.";
            },
            PdfCacheScopes.None,
            cancellationToken);
    }

    /// <inheritdoc/>
    public ValueTask<DocumentEditResult> MoveWidgetAsync(string name, int widgetIndex, PageRect bounds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        return HyperPdfDocumentEdits.RunCheckedAsync(
            _owner,
            new MoveStep(name, widgetIndex, bounds, cancellationToken),
            HyperPdfEditPrefetch.PagesAsync,
            static (document, step) =>
            {
                var field = FindField(document, step.Name, step.CancellationToken);
                if (field is null || (uint)step.WidgetIndex >= (uint)field.Widgets.Length)
                {
                    return "That widget is not in the form.";
                }

                var page = HyperPdfFormDesignMapping.GetPage(document, field.Widgets[step.WidgetIndex].Location.PageIndex);
                var bounds = HyperPdfAnnotationReading.ToUserRectangle(page, step.Bounds);
                var result = PdfFormFieldEditing.SetWidgetBounds(document, step.Name, step.WidgetIndex, bounds, step.CancellationToken);
                return result.Applied ? null : result.Reason ?? "The widget could not be moved.";
            },
            PdfCacheScopes.None,
            cancellationToken);
    }

    /// <summary>Finds a field by its fully qualified name.</summary>
    /// <param name="document">The library document.</param>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The field, or <see langword="null"/>.</returns>
    private static PdfFieldDetails? FindField(HyperPdfLibrary.Document.PdfDocument document, string name, CancellationToken cancellationToken)
    {
        var fields = PdfFormFieldEditing.GetFields(document, cancellationToken);
        foreach (var field in fields)
        {
            if (string.Equals(field.Name, name, StringComparison.Ordinal))
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>Reads the fields while no edit runs, so they are read whole or not at all.</summary>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The fields.</returns>
    private FormFieldInfo[] ReadFields(CancellationToken cancellationToken)
    {
        using var access = HyperPdfNavigation.EnterPageRead(_owner);
        ObjectDisposedException.ThrowIf(_owner.IsDisposed, _owner);
        var details = PdfFormFieldEditing.GetFields(_owner.Document, cancellationToken);
        var fields = new FormFieldInfo[details.Length];
        for (var i = 0; i < fields.Length; i++)
        {
            fields[i] = HyperPdfFormDesignMapping.ToInfo(_owner.Document, details[i]);
        }

        return fields;
    }

    /// <summary>The arguments of a create.</summary>
    /// <param name="Design">The field.</param>
    /// <param name="CancellationToken">Cancels the edit.</param>
    private readonly record struct DesignStep(FormFieldDesign Design, CancellationToken CancellationToken);

    /// <summary>The arguments of an edit that names one field.</summary>
    /// <param name="Name">The field.</param>
    /// <param name="CancellationToken">Cancels the edit.</param>
    private readonly record struct NameStep(string Name, CancellationToken CancellationToken);

    /// <summary>The arguments of a rename.</summary>
    /// <param name="Name">The field.</param>
    /// <param name="NewName">The new name.</param>
    /// <param name="CancellationToken">Cancels the edit.</param>
    private readonly record struct RenameStep(string Name, string NewName, CancellationToken CancellationToken);

    /// <summary>The arguments of a change.</summary>
    /// <param name="Name">The field.</param>
    /// <param name="Changes">The changes.</param>
    /// <param name="CancellationToken">Cancels the edit.</param>
    private readonly record struct ChangeStep(string Name, FormFieldChanges Changes, CancellationToken CancellationToken);

    /// <summary>The arguments of a move.</summary>
    /// <param name="Name">The field.</param>
    /// <param name="WidgetIndex">The widget.</param>
    /// <param name="Bounds">The new rectangle.</param>
    /// <param name="CancellationToken">Cancels the edit.</param>
    private readonly record struct MoveStep(string Name, int WidgetIndex, PageRect Bounds, CancellationToken CancellationToken);
}
