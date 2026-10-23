// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Builds a long form for the form benchmarks: many pages of text fields, one page with a combo box of 250 options and one
/// page with a group of 30 radio buttons.
/// </summary>
internal static class FormScaleFixture
{
    /// <summary>The pages in the form.</summary>
    internal const int Pages = 100;

    /// <summary>The text fields on each page.</summary>
    internal const int TextFieldsPerPage = 5;

    /// <summary>The page index that holds the combo box.</summary>
    internal const int ComboPage = 0;

    /// <summary>The page index that holds the radio group.</summary>
    internal const int RadioPage = 1;

    /// <summary>The options of the combo box.</summary>
    private const int ComboOptions = 250;

    /// <summary>The buttons of the radio group.</summary>
    private const int RadioButtons = 30;

    /// <summary>The object number of the first page.</summary>
    private const int FirstPage = 4;

    /// <summary>The top of the first widget on a page.</summary>
    private const int PageTop = 700;

    /// <summary>The distance between text fields.</summary>
    private const int TextSpacing = 30;

    /// <summary>The top of the combo box.</summary>
    private const int ComboTop = 400;

    /// <summary>The height of a widget.</summary>
    private const int WidgetHeight = 20;

    /// <summary>The distance between radio buttons.</summary>
    private const int RadioSpacing = 20;

    /// <summary>The size of a radio button.</summary>
    private const int RadioSize = 16;

    /// <summary>Builds the form.</summary>
    /// <returns>The file.</returns>
    internal static byte[] Build()
    {
        var parts = new Parts();
        AddTextFields(parts);
        AddComboBox(parts);
        AddRadioGroup(parts);
        return MiniPdf.Build([.. parts.Assemble()]);
    }

    /// <summary>Adds the text fields of every page.</summary>
    /// <param name="parts">The parts.</param>
    private static void AddTextFields(Parts parts)
    {
        for (var page = 0; page < Pages; page++)
        {
            for (var i = 0; i < TextFieldsPerPage; i++)
            {
                parts.AddTopLevel(page, Widget($"/FT /Tx /T (Text_{page}_{i}) /V (value {page} {i})", FirstPage + page, PageTop - (i * TextSpacing)));
            }
        }
    }

    /// <summary>Adds the combo box.</summary>
    /// <param name="parts">The parts.</param>
    private static void AddComboBox(Parts parts)
    {
        var options = new StringBuilder();
        for (var i = 0; i < ComboOptions; i++)
        {
            _ = options.Append(CultureInfo.InvariantCulture, $"(Option {i}) ");
        }

        var entries = $"/FT /Ch /Ff 131072 /T (Combo) /V (Option {ComboOptions - 1}) /Opt [{options}]";
        parts.AddTopLevel(ComboPage, Widget(entries, FirstPage + ComboPage, ComboTop));
    }

    /// <summary>Adds the radio group and its buttons.</summary>
    /// <param name="parts">The parts.</param>
    private static void AddRadioGroup(Parts parts)
    {
        var groupNumber = parts.NextNumber;
        var kids = new StringBuilder();
        var kidObjects = new List<string>();
        for (var i = 0; i < RadioButtons; i++)
        {
            var kidNumber = groupNumber + 1 + i;
            _ = kids.Append(CultureInfo.InvariantCulture, $"{kidNumber} 0 R ");
            parts.AddAnnotation(RadioPage, kidNumber);
            kidObjects.Add(RadioKid(groupNumber, PageTop - (i * RadioSpacing), i == RadioButtons - 1));
        }

        parts.AddField($"<< /FT /Btn /Ff 32768 /T (Radio) /V /On /Kids [{kids}] >>", groupNumber);
        parts.AddObjects(kidObjects);
    }

    /// <summary>Writes one button of the radio group.</summary>
    /// <param name="groupNumber">The object number of the group field.</param>
    /// <param name="top">The top of the button.</param>
    /// <param name="selected">Whether the button is the selected one.</param>
    /// <returns>The object body.</returns>
    private static string RadioKid(int groupNumber, int top, bool selected) =>
        $"<< /Type /Annot /Subtype /Widget /Rect [72 {top} 90 {top + RadioSize}] /P {FirstPage + RadioPage} 0 R /F 4 /Parent {groupNumber} 0 R /AS {(selected ? "/On" : "/Off")} >>";

    /// <summary>Writes a merged field and widget annotation.</summary>
    /// <param name="entries">The field entries.</param>
    /// <param name="pageNumber">The object number of the page it sits on.</param>
    /// <param name="top">The top of the widget.</param>
    /// <returns>The object body.</returns>
    private static string Widget(string entries, int pageNumber, int top) =>
        $"<< /Type /Annot /Subtype /Widget /Rect [72 {top - WidgetHeight} 272 {top}] /P {pageNumber} 0 R /F 4 {entries} >>";

    /// <summary>The objects, field list and page annotations collected while the form is built.</summary>
    private sealed class Parts
    {
        /// <summary>The field and widget objects, which follow the page objects.</summary>
        private readonly List<string> _fieldObjects = [];

        /// <summary>The top level fields.</summary>
        private readonly StringBuilder _fields = new();

        /// <summary>The annotations of each page.</summary>
        private readonly StringBuilder[] _annotations = [.. Enumerable.Range(0, Pages).Select(static _ => new StringBuilder())];

        /// <summary>Gets the object number the next field object gets.</summary>
        internal int NextNumber => FirstPage + Pages + _fieldObjects.Count;

        /// <summary>Adds a widget that is a top level field and sits on a page.</summary>
        /// <param name="page">The page index.</param>
        /// <param name="body">The widget object.</param>
        internal void AddTopLevel(int page, string body)
        {
            var number = NextNumber;
            _fieldObjects.Add(body);
            _ = _fields.Append(CultureInfo.InvariantCulture, $"{number} 0 R ");
            AddAnnotation(page, number);
        }

        /// <summary>Adds a field that has no widget of its own.</summary>
        /// <param name="body">The field object.</param>
        /// <param name="number">The object number it gets.</param>
        internal void AddField(string body, int number)
        {
            _fieldObjects.Add(body);
            _ = _fields.Append(CultureInfo.InvariantCulture, $"{number} 0 R ");
        }

        /// <summary>Adds objects that are not top level fields.</summary>
        /// <param name="bodies">The object bodies.</param>
        internal void AddObjects(List<string> bodies) => _fieldObjects.AddRange(bodies);

        /// <summary>Adds an annotation to a page.</summary>
        /// <param name="page">The page index.</param>
        /// <param name="number">The annotation's object number.</param>
        internal void AddAnnotation(int page, int number) => _ = _annotations[page].Append(CultureInfo.InvariantCulture, $"{number} 0 R ");

        /// <summary>Lists every object of the file in object number order.</summary>
        /// <returns>The object bodies.</returns>
        internal List<string> Assemble()
        {
            var pageRefs = new StringBuilder();
            for (var page = 0; page < Pages; page++)
            {
                _ = pageRefs.Append(CultureInfo.InvariantCulture, $"{FirstPage + page} 0 R ");
            }

            List<string> objects =
            [
                $"<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [{_fields}] /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv 3 0 R >> >> >> >>",
                $"<< /Type /Pages /Kids [{pageRefs}] /Count {Pages} >>",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            ];
            for (var page = 0; page < Pages; page++)
            {
                objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /Helv 3 0 R >> >> /Annots [{_annotations[page]}] >>");
            }

            objects.AddRange(_fieldObjects);
            return objects;
        }
    }
}
