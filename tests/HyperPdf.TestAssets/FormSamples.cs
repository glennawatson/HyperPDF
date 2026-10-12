// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>Writes forms that exercise field types, inheritance and appearance generation, beyond those of <see cref="TestPdf"/>.</summary>
public static class FormSamples
{
    /// <summary>The index of the multi-line text field "Notes".</summary>
    public static readonly int NotesIndex;

    /// <summary>The index of the password field "Secret".</summary>
    public static readonly int SecretIndex = 1;

    /// <summary>The index of the multi-select list box "Pick".</summary>
    public static readonly int PickIndex = 2;

    /// <summary>The index of the combo box "Colour", whose options are export value and label pairs.</summary>
    public static readonly int ColourIndex = 3;

    /// <summary>The index of the text field "Sideways", rotated a quarter turn.</summary>
    public static readonly int SidewaysIndex = 4;

    /// <summary>The index of the first radio button of "Plan", which has no appearance.</summary>
    public static readonly int PlanFirstIndex = 5;

    /// <summary>The index of the second radio button of "Plan".</summary>
    public static readonly int PlanSecondIndex = 6;

    /// <summary>The index of the read-only field "Locked".</summary>
    public static readonly int LockedIndex = 7;

    /// <summary>The index of the field "Limited", which takes five characters.</summary>
    public static readonly int LimitedIndex = 8;

    /// <summary>The index of the check box "Agree", which has no appearance.</summary>
    public static readonly int AgreeIndex = 9;

    /// <summary>The index of the field "Address.Street", whose type and value come from its parent.</summary>
    public static readonly int StreetIndex = 10;

    /// <summary>The index of the comb field "Zip", whose limit and flags come from its parent.</summary>
    public static readonly int ZipIndex = 11;

    /// <summary>The number of widgets in <see cref="CreateRichForm"/>.</summary>
    public static readonly int WidgetCount = 12;

    /// <summary>The page width.</summary>
    private const int PageWidth = 612;

    /// <summary>The page height.</summary>
    private const int PageHeight = 792;

    /// <summary>Creates a one page form with the widgets numbered by the index constants of this class.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateRichForm()
    {
        var objects = new List<string>();
        var catalog = Reserve(objects);
        var pages = Reserve(objects);
        var page = Reserve(objects);
        var font = Add(objects, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        var notes = Add(objects, Widget(page, "/FT /Tx /Ff 4096 /T (Notes) /DA (/Helv 0 Tf 0 g) /MK << /BC [0 0 0] /BG [1 1 0.8] >>", "72 640 272 740"));
        var secret = Add(objects, Widget(page, "/FT /Tx /Ff 8192 /T (Secret) /DA (/Helv 12 Tf 0 0 1 rg) /V (hidden)", "72 600 272 624"));
        var pick = Add(objects, Widget(page, "/FT /Ch /Ff 2097152 /T (Pick) /DA (/Helv 11 Tf 0 g) /Opt [(One) (Two) (Three) (Four)] /V (Two) /I [1] /MK << /BC [0] >>", "72 480 272 580"));
        var colour = Add(objects, Widget(page, "/FT /Ch /Ff 131072 /T (Colour) /DA (/Helv 12 Tf 0 g) /Opt [[(r) (Red)] [(g) (Green)]] /V (g)", "72 440 272 464"));
        var sideways = Add(objects, Widget(page, "/FT /Tx /T (Sideways) /DA (/Helv 12 Tf 0 g) /MK << /R 90 /BC [0 0 0] >> /V (Turned)", "300 440 324 560"));
        var plan = Reserve(objects);
        var planFirst = Add(objects, Widget(page, $"/Parent {plan} 0 R", "72 400 88 416"));
        var planSecond = Add(objects, Widget(page, $"/Parent {plan} 0 R", "120 400 136 416"));
        objects[plan - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /FT /Btn /Ff 32768 /T (Plan) /Kids [{planFirst} 0 R {planSecond} 0 R] >>");
        var locked = Add(objects, Widget(page, "/FT /Tx /Ff 1 /T (Locked) /DA (/Helv 12 Tf 0 g) /V (Fixed)", "72 360 272 384"));
        var limited = Add(objects, Widget(page, "/FT /Tx /MaxLen 5 /T (Limited) /DA (/Helv 12 Tf 0 g)", "72 320 272 344"));
        var agree = Add(objects, Widget(page, "/FT /Btn /T (Agree)", "72 280 88 296"));
        var address = Reserve(objects);
        var street = Reserve(objects);
        var streetWidget = Add(objects, Widget(page, $"/Parent {street} 0 R", "72 240 272 264"));
        objects[street - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Parent {address} 0 R /T (Street) /FT /Tx /V (Main St) /DA (/Helv 10 Tf 0 g) /Kids [{streetWidget} 0 R] >>");
        objects[address - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /T (Address) /Kids [{street} 0 R] >>");
        var zip = Reserve(objects);
        var zipWidget = Add(objects, Widget(page, $"/Parent {zip} 0 R /MK << /BC [0 0 0] >>", "72 200 172 224"));
        objects[zip - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /FT /Tx /Ff 16777216 /MaxLen 5 /T (Zip) /DA (/Helv 12 Tf 0 g) /Kids [{zipWidget} 0 R] >>");
        var annots = References(notes, secret, pick, colour, sideways, planFirst, planSecond, locked, limited, agree, streetWidget, zipWidget);
        var fields = References(notes, secret, pick, colour, sideways, plan, locked, limited, agree, address, zip);
        objects[page - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Page /Parent {{pages}} 0 R /MediaBox [0 0 {{PageWidth}} {{PageHeight}}]
               /Resources << /Font << /Helv {{font}} 0 R >> >> /Annots [{{annots}}] >>
            """);
        objects[pages - 1] = string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{page} 0 R] /Count 1 >>");
        objects[catalog - 1] = string.Create(CultureInfo.InvariantCulture, $$"""
            << /Type /Catalog /Pages {{pages}} 0 R
               /AcroForm << /Fields [{{fields}}] /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv {{font}} 0 R >> >> >> >>
            """);
        return Serialize(objects, catalog);
    }

    /// <summary>Writes a list of references.</summary>
    /// <param name="numbers">The object numbers.</param>
    /// <returns>The references separated by spaces.</returns>
    private static string References(params int[] numbers) =>
        string.Join(' ', numbers.Select(static number => string.Create(CultureInfo.InvariantCulture, $"{number} 0 R")));

    /// <summary>Writes a widget annotation dictionary.</summary>
    /// <param name="page">The page's object number.</param>
    /// <param name="entries">The widget's other entries.</param>
    /// <param name="rect">The rectangle's four numbers.</param>
    /// <returns>The dictionary.</returns>
    private static string Widget(int page, string entries, string rect) =>
        string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Widget /Rect [{rect}] /P {page} 0 R /F 4 {entries} >>");

    /// <summary>Adds an empty object whose body is written later.</summary>
    /// <param name="objects">The object list.</param>
    /// <returns>The object number.</returns>
    private static int Reserve(List<string> objects) => Add(objects, string.Empty);

    /// <summary>Adds an object.</summary>
    /// <param name="objects">The object list.</param>
    /// <param name="body">The object body.</param>
    /// <returns>The object number.</returns>
    private static int Add(List<string> objects, string body)
    {
        objects.Add(body);
        return objects.Count;
    }

    /// <summary>Writes the objects with a cross-reference table.</summary>
    /// <param name="objects">The object bodies, numbered from 1.</param>
    /// <param name="root">The catalog's object number.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Serialize(List<string> objects, int root)
    {
        var output = new StringBuilder("%PDF-1.7\n");
        var offsets = new int[objects.Count];
        for (var i = 0; i < objects.Count; i++)
        {
            offsets[i] = Encoding.ASCII.GetByteCount(output.ToString());
            _ = output.Append(CultureInfo.InvariantCulture, $"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = Encoding.ASCII.GetByteCount(output.ToString());
        _ = output.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            _ = output.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
        }

        _ = output.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Count + 1} /Root {root} 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(output.ToString());
    }
}
