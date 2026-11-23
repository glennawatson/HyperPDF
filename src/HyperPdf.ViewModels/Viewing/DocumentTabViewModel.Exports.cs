// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Text;
using HyperPdf.ViewModels.FormFilling;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>Changing the document's details, and writing its form answers or its text to another file.</summary>
public sealed partial class DocumentTabViewModel
{
    /// <summary>Gets the interaction that shows the window of the document's details. It answers whether they were accepted.</summary>
    public Interaction<DocumentInfoViewModel, bool> DocumentInfoInteraction { get; } = new();

    /// <summary>Gets the interaction asking where to save an exported file; the input is the suggested name.</summary>
    public Interaction<string, string?> ExportInteraction { get; } = new();

    /// <summary>Gets the interaction asking which FDF or XFDF file to read a form's answers from.</summary>
    public Interaction<RxVoid, string?> ImportInteraction { get; } = new();

    /// <summary>Gets the name the document's exports start with: its file name without the extension.</summary>
    private string ExportName => Path.GetFileNameWithoutExtension(FilePath) is { Length: > 0 } name ? name : UiText.Get("document");

    /// <summary>Gets the end of an exported text file's name.</summary>
    /// <param name="format">The kind of file.</param>
    /// <returns>The extension.</returns>
    private static string ExtensionOf(DocumentTextFormat format) => format switch
    {
        DocumentTextFormat.Html => ".html",
        DocumentTextFormat.Markdown => ".md",
        _ => ".txt",
    };

    /// <summary>Writes bytes to a file, so the window keeps working while a large file is written.</summary>
    /// <param name="path">The file.</param>
    /// <param name="bytes">The bytes.</param>
    /// <param name="cancellationToken">Stops the work.</param>
    /// <returns>The path written.</returns>
    private static async Task<string> WriteBytesAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        await File.WriteAllBytesAsync(path, bytes, cancellationToken).ConfigureAwait(false);
        return path;
    }

    /// <summary>Writes the document's text to a file as UTF-8.</summary>
    /// <param name="exporter">The document's text exporter.</param>
    /// <param name="path">The file.</param>
    /// <param name="format">The kind of file.</param>
    /// <param name="cancellationToken">Stops the work between pages.</param>
    /// <returns>The path written.</returns>
    private static async Task<string> WriteTextAsync(IDocumentTextExporter exporter, string path, DocumentTextFormat format, CancellationToken cancellationToken)
    {
        var output = new StreamWriter(path, false, new UTF8Encoding(false));
        await using (output.ConfigureAwait(false))
        {
            await exporter.WriteAsync(output, format, cancellationToken).ConfigureAwait(false);
        }

        return path;
    }

    /// <summary>Shows the document's title, author, subject and keywords to change, then writes them as one step that can be undone.</summary>
    /// <returns>A signal that ends once the details are written or the window is closed.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> EditDocumentInfo()
    {
        if (DocumentFeature<IDocumentInfoEditor>.Get(TryGetDocument()) is not { } editor)
        {
            SetNotice(LocalizedMessage.Get(UiText, "This document's details cannot be changed."));
            return Signal.Return(RxVoid.Default);
        }

        var details = new DocumentInfoViewModel(Source.Metadata ?? new DocumentMetadata());
        var refused = UiText.Get("The document's details could not be changed.");
        var done = UiText.Get("Changed the document's details. Save to keep them.");
        return DocumentInfoInteraction.Handle(details).ToSignal()
            .SwitchMap(accepted => !accepted
                ? Signal.Return(RxVoid.Default)
                : editor.SetInfo(details.ToEdit())
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Select(result => Exported(result.Succeeded, result.Problem ?? refused, done)));
    }

    /// <summary>Asks where to save the form's answers, then writes them as FDF or XFDF.</summary>
    /// <param name="format">The kind of file.</param>
    /// <returns>A signal that ends once the file is written or the choice is given up.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> ExportFormData(FormDataFormat format)
    {
        if (DocumentFeature<IFormDataExporter>.Get(TryGetDocument()) is not { } exporter)
        {
            SetNotice(LocalizedMessage.Get(UiText, "This document's form answers cannot be saved."));
            return Signal.Return(RxVoid.Default);
        }

        var name = string.Concat(ExportName, format == FormDataFormat.Xfdf ? ".xfdf" : ".fdf");
        return ExportInteraction.Handle(name).ToSignal()
            .SwitchMap(path => string.IsNullOrEmpty(path)
                ? Signal.Return(RxVoid.Default)
                : Signal.FromAsync(token => WriteBytesAsync(path, exporter.Export(format), token))
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Select(written => Exported(true, string.Empty, UiText.Format("Saved the form's answers to {0}.", Path.GetFileName(written)))))
            .Catch<RxVoid, IOException>(error => ExportFailed(error.Message))
            .Catch<RxVoid, UnauthorizedAccessException>(error => ExportFailed(error.Message));
    }

    /// <summary>Asks which FDF or XFDF file to read, then fills the form with its answers as one step that can be undone.</summary>
    /// <returns>A signal that ends once the answers are in or the choice is given up.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> ImportFormData()
    {
        if (DocumentFeature<IFormDataImporter>.Get(TryGetDocument()) is not { } importer)
        {
            SetNotice(LocalizedMessage.Get(UiText, "This document's form answers cannot be loaded."));
            return Signal.Return(RxVoid.Default);
        }

        return ImportInteraction.Handle(RxVoid.Default).ToSignal()
            .SwitchMap(path => string.IsNullOrEmpty(path)
                ? Signal.Return(RxVoid.Default)
                : FormDataFiles.Read(path).ObserveOn(RxSchedulers.MainThreadScheduler).Select(data => Imported(path, data, importer)));
    }

    /// <summary>Asks where to save the document's text, then writes every page's text as plain text, Markdown or HTML.</summary>
    /// <param name="format">The kind of file.</param>
    /// <returns>A signal that ends once the file is written or the choice is given up.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> ExportText(DocumentTextFormat format)
    {
        if (DocumentFeature<IDocumentTextExporter>.Get(TryGetDocument()) is not { } exporter)
        {
            SetNotice(LocalizedMessage.Get(UiText, "This document's text cannot be saved."));
            return Signal.Return(RxVoid.Default);
        }

        return ExportInteraction.Handle(string.Concat(ExportName, ExtensionOf(format))).ToSignal()
            .SwitchMap(path => string.IsNullOrEmpty(path)
                ? Signal.Return(RxVoid.Default)
                : Signal.FromAsync(token => WriteTextAsync(exporter, path, format, token))
                    .ObserveOn(RxSchedulers.MainThreadScheduler)
                    .Select(written => Exported(true, string.Empty, UiText.Format("Saved the text to {0}.", Path.GetFileName(written)))))
            .Catch<RxVoid, IOException>(error => ExportFailed(error.Message))
            .Catch<RxVoid, UnauthorizedAccessException>(error => ExportFailed(error.Message));
    }

    /// <summary>Says what an export or a change of details did.</summary>
    /// <param name="succeeded">Whether it worked.</param>
    /// <param name="problem">What went wrong, when it did not.</param>
    /// <param name="done">What to say when it worked.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Exported(bool succeeded, string problem, string done)
    {
        if (succeeded)
        {
            Pages.Refresh();
        }

        SetNotice(LocalizedMessage.Literal(succeeded ? done : problem));
        return RxVoid.Default;
    }

    /// <summary>Fills the form with a file's answers and says what changed.</summary>
    /// <param name="path">The file.</param>
    /// <param name="data">Its bytes, or <see langword="null"/> when it could not be read.</param>
    /// <param name="importer">The document's form data importer.</param>
    /// <returns>The completion value.</returns>
    private RxVoid Imported(string path, byte[]? data, IFormDataImporter importer)
    {
        var result = data is null ? FormDataImportResult.Failed : importer.Import(data);
        if (result.Succeeded)
        {
            Pages.Refresh();
        }

        SetNotice(LocalizedMessage.Literal(FormDataFiles.Describe(Path.GetFileName(path), result)));
        return RxVoid.Default;
    }

    /// <summary>Says why a file could not be written.</summary>
    /// <param name="reason">Why.</param>
    /// <returns>A signal that ends at once.</returns>
    private IObservable<RxVoid> ExportFailed(string reason)
    {
        SetNotice(LocalizedMessage.Format(UiText, "Could not save the file: {0}", reason));
        return Signal.Return(RxVoid.Default);
    }
}
