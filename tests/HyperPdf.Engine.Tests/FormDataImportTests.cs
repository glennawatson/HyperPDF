// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Checks a form's Import Data action: the host is offered the file, the engine applies the chosen XFDF or FDF file as one
/// undoable edit, and a file that is not valid changes nothing.
/// </summary>
public sealed class FormDataImportTests
{
    /// <summary>The value the file gives the "Name" field.</summary>
    private const string ImportedName = "new";

    /// <summary>The fields of <see cref="Answers"/> that the form has.</summary>
    private const int AppliedFields = 2;

    /// <summary>Gets an XFDF file with two fields the form has and one it has not.</summary>
    private static ReadOnlySpan<byte> Answers => """
        <?xml version="1.0" encoding="UTF-8"?>
        <xfdf xmlns="http://ns.adobe.com/xfdf/"><fields>
          <field name="Name"><value>new</value></field>
          <field name="Note"><value>a note</value></field>
          <field name="Missing"><value>x</value></field>
        </fields></xfdf>
        """u8;

    /// <summary>The importer applies an XFDF file, ignores unknown fields, and reports both.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImporterAppliesAnXfdfFile()
    {
        using var pair = new EnginePair(FormImportSamples.Create());
        var versionBefore = pair.HyperPdf.EditVersion;

        var result = new HyperPdfFormDataImporter(pair.HyperPdf).Import(Answers.ToArray());

        await Assert.That(result.Succeeded).IsTrue();
        await Assert.That(result.FieldsApplied).IsEqualTo(AppliedFields);
        await Assert.That(result.FieldsSkipped).IsEqualTo(1);
        await Assert.That(ValueOf(pair, FormImportSamples.NameIndex)).IsEqualTo(ImportedName);
        await Assert.That(ValueOf(pair, FormImportSamples.NoteIndex)).IsEqualTo("a note");
        await Assert.That(pair.HyperPdf.EditVersion).IsGreaterThan(versionBefore);
    }

    /// <summary>The import is one undo step named "Import form data", and undoing it puts the old values back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImportIsOneUndoableEdit()
    {
        using var pair = new EnginePair(FormImportSamples.Create());
        _ = new HyperPdfFormDataImporter(pair.HyperPdf).Import(Answers.ToArray());

        await Assert.That(PdfDocumentEditing.GetHistory(pair.Library).UndoLabel).IsEqualTo(HyperPdfFormDataImporter.UndoLabel);
        await Assert.That(PdfDocumentEditing.Undo(pair.Library)).IsTrue();
        await Assert.That(ValueOf(pair, FormImportSamples.NameIndex)).IsEqualTo(FormImportSamples.OriginalName);
        await Assert.That(ValueOf(pair, FormImportSamples.NoteIndex)).IsEqualTo(string.Empty);
        await Assert.That(PdfDocumentEditing.GetHistory(pair.Library).UndoLabel).IsNull();
        await Assert.That(PdfDocumentEditing.Redo(pair.Library)).IsTrue();
        await Assert.That(ValueOf(pair, FormImportSamples.NameIndex)).IsEqualTo(ImportedName);
    }

    /// <summary>Text that is not FDF or XFDF fails, leaves the form as it was and adds nothing to the undo history.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidFilesChangeNothing()
    {
        using var pair = new EnginePair(FormImportSamples.Create());
        var importer = new HyperPdfFormDataImporter(pair.HyperPdf);
        var versionBefore = pair.HyperPdf.EditVersion;

        var notXml = importer.Import("this is not form data"u8.ToArray());
        var doctype = importer.Import(
            """
            <?xml version="1.0"?><!DOCTYPE xfdf [<!ENTITY e "x">]>
            <xfdf xmlns="http://ns.adobe.com/xfdf/"><fields><field name="Name"><value>&e;</value></field></fields></xfdf>
            """u8.ToArray());
        var empty = importer.Import(default);

        await Assert.That(notXml).IsEqualTo(FormDataImportResult.Failed);
        await Assert.That(doctype).IsEqualTo(FormDataImportResult.Failed);
        await Assert.That(empty).IsEqualTo(FormDataImportResult.Failed);
        await Assert.That(ValueOf(pair, FormImportSamples.NameIndex)).IsEqualTo(FormImportSamples.OriginalName);
        await Assert.That(PdfDocumentEditing.GetHistory(pair.Library).UndoLabel).IsNull();
        await Assert.That(pair.HyperPdf.EditVersion).IsEqualTo(versionBefore);
    }

    /// <summary>
    /// The button's action reaches the host with the file name the document gave. A host that imports through the importer it
    /// is handed reports the form changed; a host that declines changes nothing.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ButtonActionOffersTheImportToTheHost()
    {
        using var pair = new EnginePair(FormImportSamples.Create());
        var accepting = new ImportingHost(Answers.ToArray());
        var declining = new ImportingHost(null);

        var actions = DocumentFeature<IFormActions>.Cast(pair.HyperPdf)!;

        var declined = await actions.RunWidgetActionAsync(0, FormImportSamples.LoadIndex, declining, CancellationToken.None);
        var valueAfterDecline = ValueOf(pair, FormImportSamples.NameIndex);
        var accepted = await actions.RunWidgetActionAsync(0, FormImportSamples.LoadIndex, accepting, CancellationToken.None);

        await Assert.That(declined).IsEqualTo(FormActionResult.Declined);
        await Assert.That(valueAfterDecline).IsEqualTo(FormImportSamples.OriginalName);
        await Assert.That(declining.Files).IsEquivalentTo([FormImportSamples.NamedFile]);
        await Assert.That(accepted).IsEqualTo(FormActionResult.Ran | FormActionResult.Changed);
        await Assert.That(accepting.Files).IsEquivalentTo([FormImportSamples.NamedFile]);
        await Assert.That(accepting.Result.Succeeded).IsTrue();
        await Assert.That(ValueOf(pair, FormImportSamples.NameIndex)).IsEqualTo(ImportedName);
    }

    /// <summary>Reads a text field's value by widget index.</summary>
    /// <param name="pair">The documents.</param>
    /// <param name="index">The widget index.</param>
    /// <returns>The value.</returns>
    private static string ValueOf(EnginePair pair, int index)
    {
        var widgets = new List<PdfFormWidget>();
        PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(pair.Library), 0, widgets);
        return widgets.Find(widget => widget.Index == index)!.Value;
    }

    /// <summary>A host that records the file the form asked for and imports a given file through the importer it is handed, or declines.</summary>
    /// <param name="data">The bytes of the file the reader chose, or <see langword="null"/> when the reader declines.</param>
    [DebuggerDisplay("ImportingHost")]
    private sealed class ImportingHost(byte[]? data) : IFormActionHost
    {
        /// <summary>Gets the file names the form asked for.</summary>
        internal List<string> Files { get; } = [];

        /// <summary>Gets what the importer reported.</summary>
        internal FormDataImportResult Result { get; private set; }

        /// <inheritdoc/>
        public ValueTask<bool> RunNamedAsync(string name, int sourcePage, CancellationToken cancellationToken) => ValueTask.FromResult(false);

        /// <inheritdoc/>
        public ValueTask<bool> OpenAsync(LinkTarget target, CancellationToken cancellationToken) => ValueTask.FromResult(false);

        /// <inheritdoc/>
        public ValueTask<bool> SubmitAsync(FormSubmission submission, CancellationToken cancellationToken) => ValueTask.FromResult(false);

        /// <inheritdoc/>
        public ValueTask<bool> ImportAsync(string? file, IFormDataImporter importer, CancellationToken cancellationToken)
        {
            Files.Add(file ?? string.Empty);
            if (data is null)
            {
                return ValueTask.FromResult(false);
            }

            Result = importer.Import(data);
            return ValueTask.FromResult(Result.Succeeded);
        }
    }
}
