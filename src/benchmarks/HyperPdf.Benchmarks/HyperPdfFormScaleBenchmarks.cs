// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures reading and editing a form that is long and has the field kinds that cost the most: a page with a combo box of
/// 250 options, a page with a group of 30 radio buttons, and many pages of text fields.
/// </summary>
public class HyperPdfFormScaleBenchmarks
{
    /// <summary>The fields changed by the edit benchmark.</summary>
    private const int EditedFields = 20;

    /// <summary>The widgets read, reused between runs.</summary>
    private readonly List<PdfFormWidget> _widgets = [];

    /// <summary>The form document.</summary>
    private PdfDocument _document = null!;

    /// <summary>The form.</summary>
    private PdfForm _form = null!;

    /// <summary>Builds the form outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(FormScaleFixture.Build(), null);
        _form = PdfDocumentForms.GetForm(_document);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Reads the widgets of the page that holds the combo box.</summary>
    /// <returns>The widgets read.</returns>
    [Benchmark]
    public int ReadComboBoxPage() => Read(FormScaleFixture.ComboPage);

    /// <summary>Reads the widgets of the page that holds the radio group.</summary>
    /// <returns>The widgets read.</returns>
    [Benchmark]
    public int ReadRadioGroupPage() => Read(FormScaleFixture.RadioPage);

    /// <summary>Reads the widgets of every page.</summary>
    /// <returns>The widgets read.</returns>
    [Benchmark]
    public int ReadEveryPage()
    {
        var count = 0;
        for (var page = 0; page < FormScaleFixture.Pages; page++)
        {
            count += Read(page);
        }

        return count;
    }

    /// <summary>Changes the tooltip of several fields, one call each, then undoes every change.</summary>
    /// <returns>The fields changed.</returns>
    [Benchmark]
    public int ChangeSeveralFields()
    {
        var changed = 0;
        for (var i = 0; i < EditedFields; i++)
        {
            var name = string.Create(CultureInfo.InvariantCulture, $"Text_{FormScaleFixture.Pages - 1}_{i % FormScaleFixture.TextFieldsPerPage}");
            if (PdfFormFieldEditing.ChangeField(_document, name, new() { Tooltip = "Benchmark" }).Applied)
            {
                changed++;
            }
        }

        for (var i = 0; i < changed; i++)
        {
            _ = PdfDocumentEditing.Undo(_document);
        }

        return changed;
    }

    /// <summary>Reads the widgets of a page.</summary>
    /// <param name="page">The page index.</param>
    /// <returns>The widgets read.</returns>
    private int Read(int page)
    {
        _widgets.Clear();
        PdfFormReading.GetWidgets(_form, page, _widgets);
        return _widgets.Count;
    }
}
