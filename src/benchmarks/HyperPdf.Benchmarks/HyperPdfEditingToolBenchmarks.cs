// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Text;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using ReactiveUI.Primitives;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures the editing tools through the engine, each on an open document: setting the document's details, attaching a file,
/// replacing the text of a line, saving the form's answers and writing the text. Each edit is
/// undone after it, and each iteration opens the text document again, because an edit kept for redo grows the document.
/// </summary>
[InvocationCount(InvocationsPerIteration)]
public class HyperPdfEditingToolBenchmarks
{
    /// <summary>The operations measured in each iteration, on one opened document.</summary>
    private const int InvocationsPerIteration = 32;

    /// <summary>The pages of the text document.</summary>
    private const int Pages = 20;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 30;

    /// <summary>The bytes of the attached file.</summary>
    private const int AttachedBytes = 4096;

    /// <summary>The details set.</summary>
    private static readonly DocumentInfoEdit Details = new("A title", "An author", "A subject", "one, two");

    /// <summary>The attached file's bytes.</summary>
    private static readonly byte[] Attached = new byte[AttachedBytes];

    /// <summary>The text document's bytes.</summary>
    private readonly byte[] _textPdf = TestPdf.CreateTextFilled(Pages, Lines);

    /// <summary>The text document.</summary>
    private HyperPdfDocument _text = null!;

    /// <summary>The form document.</summary>
    private HyperPdfDocument _form = null!;

    /// <summary>The text document's page manager, which also undoes every edit of the document.</summary>
    private IPageManager _manager = null!;

    /// <summary>Opens both documents and runs each tool once.</summary>
    /// <returns>A task completing after the warm runs.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _form = new(PdfDocumentReader.Open(TestPdf.CreateForm(), null), "form.pdf");
        OpenText();
        _ = await SetDetails().ConfigureAwait(false);
        _ = await AttachFile().ConfigureAwait(false);
        _ = await ReplaceLineText().ConfigureAwait(false);
        _ = ExportFormAnswersFdf();
        _ = ExportFormAnswersXfdf();
        await ExportText(DocumentTextFormat.PlainText).ConfigureAwait(false);
    }

    /// <summary>Opens the text document for this iteration.</summary>
    [IterationSetup]
    public void OpenText()
    {
        _text?.Dispose();
        _text = new(PdfDocumentReader.Open(_textPdf, null), "editing-tools.pdf");
        _manager = HyperPdfDocumentPageManagement.GetPageManager(_text);
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _text.Dispose();
        _form.Dispose();
    }

    /// <summary>Sets the title, author, subject and keywords, then undoes it.</summary>
    /// <returns>Whether the edit was made.</returns>
    [Benchmark(Baseline = true)]
    public async ValueTask<bool> SetDetails()
    {
        var result = await DocumentFeature<IDocumentInfoEditor>.Cast(_text)!.SetInfo(Details).ToTask().ConfigureAwait(false);
        await Undo().ConfigureAwait(false);
        return result.Succeeded;
    }

    /// <summary>Attaches a file, then undoes it.</summary>
    /// <returns>Whether the file was attached.</returns>
    [Benchmark]
    public async ValueTask<bool> AttachFile()
    {
        var result = await DocumentFeature<IAttachmentEditor>.Cast(_text)!.AddAttachment("notes.bin", Attached).ToTask().ConfigureAwait(false);
        await Undo().ConfigureAwait(false);
        return result.Succeeded;
    }

    /// <summary>Reads the first page's objects and replaces the text of its first line, as find and replace does, then undoes it.</summary>
    /// <returns>The objects on the page.</returns>
    [Benchmark]
    public async ValueTask<int> ReplaceLineText()
    {
        var editor = DocumentFeature<IPageContentEditor>.Cast(_text)!;
        var objects = await editor.ReadObjects(0).ToTask().ConfigureAwait(false);
        foreach (var item in objects)
        {
            if (item.Kind != PageObjectKind.Text)
            {
                continue;
            }

            _ = await editor.ReplaceText(0, item.Index, item.Text.Replace("brown", "red", StringComparison.Ordinal)).ToTask().ConfigureAwait(false);
            break;
        }

        await Undo().ConfigureAwait(false);
        return objects.Length;
    }

    /// <summary>Writes the form's answers as FDF.</summary>
    /// <returns>The file's length.</returns>
    [Benchmark]
    public int ExportFormAnswersFdf() => DocumentFeature<IFormDataExporter>.Cast(_form)!.Export(FormDataFormat.Fdf).Length;

    /// <summary>Writes the form's answers as XFDF.</summary>
    /// <returns>The file's length.</returns>
    [Benchmark]
    public int ExportFormAnswersXfdf() => DocumentFeature<IFormDataExporter>.Cast(_form)!.Export(FormDataFormat.Xfdf).Length;

    /// <summary>Writes the text of every page in a format, to a writer that keeps nothing.</summary>
    /// <param name="format">The kind of file.</param>
    /// <returns>A task completing once every page is written.</returns>
    [Benchmark]
    [Arguments(DocumentTextFormat.PlainText)]
    [Arguments(DocumentTextFormat.Markdown)]
    [Arguments(DocumentTextFormat.Html)]
    public Task ExportText(DocumentTextFormat format) =>
        DocumentFeature<IDocumentTextExporter>.Cast(_text)!.WriteAsync(TextWriter.Null, format, CancellationToken.None);

    /// <summary>Undoes the last edit of the text document.</summary>
    /// <returns>A task completing once it is undone.</returns>
    /// <exception cref="InvalidOperationException">There was nothing to undo.</exception>
    private async ValueTask Undo()
    {
        if (!await _manager.Undo().ToTask().ConfigureAwait(false))
        {
            throw new InvalidOperationException("The edit could not be undone.");
        }
    }
}
