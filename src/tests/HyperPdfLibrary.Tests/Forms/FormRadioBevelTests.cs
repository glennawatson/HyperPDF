// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests the round bevel and inset borders of radio buttons (ISO 32000-2 12.5.4, Table 168).</summary>
public sealed class FormRadioBevelTests
{
    /// <summary>The flags of a radio button: radio and no toggle to off.</summary>
    private const string Radio = "/FT /Btn /Ff 49152 /T (Group)";

    /// <summary>The square widget the tests use.</summary>
    private const string Square = "/Rect [20 200 50 230]";

    /// <summary>The two half arcs of a bevel each rotate the arc's coordinates, and the mark scales them once more.</summary>
    private const int ArcsAndMark = 3;

    /// <summary>A bevel draws the background circle of four curves and two arcs of two curves; the mark is a circle of four.</summary>
    private const int CurvesOfBevelAndMark = 12;

    /// <summary>A beveled border has a lit upper left arc, a shaded lower right arc and a ring, each drawn with a curve path.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BeveledRadioButtonHasTwoHalfArcsAndARing()
    {
        using var document = FormTestPdf.Open(
            FormTestPdf.Helvetica,
            $"{Radio} /BS << /W 2 /S /B >> /MK << /BC [0 0 0] /BG [0.8 0.8 0.8] >> {Square}");

        var content = OnContent(document);

        await Assert.That(content).Contains("0.5 0.5 0.5 RG");
        await Assert.That(content).Contains("0 0 0 RG");
        await Assert.That(Count(content, " cm\n")).IsEqualTo(ArcsAndMark);
        await Assert.That(Count(content, " c\n")).IsEqualTo(CurvesOfBevelAndMark);
    }

    /// <summary>An inset border puts the dark tone on the upper left arc and the light tone on the lower right one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsetRadioButtonReversesTheTones()
    {
        using var document = FormTestPdf.Open(
            FormTestPdf.Helvetica,
            $"{Radio} /BS << /W 1 /S /I >> /MK << /BC [0 0 0] >> {Square}");

        var content = OnContent(document);

        await Assert.That(content.IndexOf("0 0 0 RG", StringComparison.Ordinal)).IsLessThan(content.IndexOf("0.5 0.5 0.5 RG", StringComparison.Ordinal));
    }

    /// <summary>A solid border is still a single ring, with no half arcs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SolidRadioButtonHasNoHalfArcs()
    {
        using var document = FormTestPdf.Open(
            FormTestPdf.Helvetica,
            $"{Radio} /BS << /W 1 /S /S >> /MK << /BC [0 0 0] >> {Square}");

        await Assert.That(Count(OnContent(document), " cm\n")).IsEqualTo(1);
    }

    /// <summary>Counts the occurrences of some text.</summary>
    /// <param name="text">The text to search.</param>
    /// <param name="part">The part to count.</param>
    /// <returns>The number of times it appears.</returns>
    private static int Count(string text, string part) => text.Split(part).Length - 1;

    /// <summary>Switches the radio button on, which draws its appearances, and reads the on appearance.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The content of the on state.</returns>
    private static string OnContent(PdfDocument document)
    {
        _ = PdfFormEditing.SetChecked(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget, true);
        var states = FormTestPdf.Annotation(document).GetDictionary(KnownName.AP)!.GetDictionary(KnownName.N)!;
        return Encoding.Latin1.GetString(states.GetStream(document.Objects.Names.Intern("Yes"))!.DecodeToArray());
    }
}
