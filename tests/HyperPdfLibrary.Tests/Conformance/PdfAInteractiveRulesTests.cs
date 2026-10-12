// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>
/// Tests for the PDF/A annotation, appearance, action, form field and optional content rules. Each case was checked
/// against veraPDF 1.30.3, which gives the same pass or fail.
/// </summary>
public sealed class PdfAInteractiveRulesTests
{
    /// <summary>ISO 19005-2 6.3.1 test 1: annotation types.</summary>
    private const string AnnotationType = "6.3.1:1";

    /// <summary>ISO 19005-2 6.3.2 test 1: the F key.</summary>
    private const string FlagsPresent = "6.3.2:1";

    /// <summary>ISO 19005-2 6.3.2 test 2: the flag bits.</summary>
    private const string FlagBits = "6.3.2:2";

    /// <summary>ISO 19005-2 6.3.3 test 1: an appearance dictionary.</summary>
    private const string AppearancePresent = "6.3.3:1";

    /// <summary>ISO 19005-2 6.3.3 test 2: only the N appearance.</summary>
    private const string OnlyNormal = "6.3.3:2";

    /// <summary>ISO 19005-2 6.3.3 test 3: button appearance subdictionaries.</summary>
    private const string ButtonSubdictionary = "6.3.3:3";

    /// <summary>ISO 19005-2 6.4.1 test 1: no widget actions.</summary>
    private const string WidgetActions = "6.4.1:1";

    /// <summary>ISO 19005-2 6.4.1 test 2: no form field additional actions.</summary>
    private const string FieldActions = "6.4.1:2";

    /// <summary>ISO 19005-2 6.5.1 test 1: action types.</summary>
    private const string ActionType = "6.5.1:1";

    /// <summary>ISO 19005-2 6.5.1 test 2: named actions.</summary>
    private const string NamedAction = "6.5.1:2";

    /// <summary>ISO 19005-4 6.6.3 test 1: trigger events.</summary>
    private const string Triggers = "6.6.3:1";

    /// <summary>ISO 19005-2 6.9 test 1: configuration names.</summary>
    private const string ConfigName = "6.9:1";

    /// <summary>ISO 19005-2 6.9 test 2: unique configuration names.</summary>
    private const string UniqueName = "6.9:2";

    /// <summary>ISO 19005-2 6.9 test 3: the order lists every group.</summary>
    private const string OrderListsGroups = "6.9:3";

    /// <summary>ISO 19005-2 6.9 test 4: no AS key.</summary>
    private const string AutoState = "6.9:4";

    /// <summary>The entries of a printable square annotation with a normal appearance, missing only its /AP target.</summary>
    private const string SquareEntries = "/Type /Annot /Subtype /Square /Rect [0 0 10 10] /F 4";

    /// <summary>An action that opens a file, which PDF/A forbids.</summary>
    private const string LaunchAction = "<< /S /Launch /F (x) >>";

    /// <summary>A go-to action, which every part allows.</summary>
    private const string GoToAction = "<< /S /GoTo /D [3 0 R /Fit] >>";

    /// <summary>An annotation type PDF/A forbids fails the type rule; an allowed one passes.</summary>
    /// <param name="subtype">The annotation subtype.</param>
    /// <param name="fails">Whether the rule fails.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Sound", true)]
    [Arguments("Movie", true)]
    [Arguments("Square", false)]
    [Arguments("FileAttachment", false)]
    public async Task AnnotationTypes(string subtype, bool fails)
    {
        var pdf = ConformancePdf.Page();
        AddAnnotation(pdf, $"/Type /Annot /Subtype /{subtype} /Rect [0 0 10 10] /F 4");

        await Assert.That(Failures(pdf, PdfALevel.PdfA2B).Contains(AnnotationType)).IsEqualTo(fails);
    }

    /// <summary>PDF/A-4 allows FileAttachment annotations only at level f.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FileAttachmentNeedsPdfA4F()
    {
        var pdf = ConformancePdf.Page();
        AddAnnotation(pdf, "/Type /Annot /Subtype /FileAttachment /Rect [0 0 10 10] /F 4");

        await Assert.That(Failures(pdf, PdfALevel.PdfA4).Contains(AnnotationType)).IsTrue();
        await Assert.That(Failures(pdf, PdfALevel.PdfA4F).Contains(AnnotationType)).IsFalse();
    }

    /// <summary>A missing F key fails except on a popup; hidden and unprintable flags fail.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationFlags()
    {
        var missing = ConformancePdf.Page();
        AddAnnotation(missing, "/Type /Annot /Subtype /Square /Rect [0 0 10 10]");
        var popup = ConformancePdf.Page();
        AddAnnotation(popup, "/Type /Annot /Subtype /Popup /Rect [0 0 10 10]");
        var hidden = ConformancePdf.Page();
        AddAnnotation(hidden, "/Type /Annot /Subtype /Square /Rect [0 0 10 10] /F 6");

        await Assert.That(Failures(missing, PdfALevel.PdfA2B).Contains(FlagsPresent)).IsTrue();
        await Assert.That(Failures(popup, PdfALevel.PdfA2B).Contains(FlagsPresent)).IsFalse();
        await Assert.That(Failures(hidden, PdfALevel.PdfA2B).Contains(FlagBits)).IsTrue();
    }

    /// <summary>An annotation needs a normal appearance and nothing else in its appearance dictionary.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Appearances()
    {
        var none = ConformancePdf.Page();
        AddAnnotation(none, SquareEntries);
        var extra = ConformancePdf.Page();
        var stream = extra.AddStream(string.Empty, string.Empty);
        AddAnnotation(extra, $"{SquareEntries} /AP << /N {stream} 0 R /D {stream} 0 R >>");
        var normal = ConformancePdf.Page();
        stream = normal.AddStream(string.Empty, string.Empty);
        AddAnnotation(normal, $"{SquareEntries} /AP << /N {stream} 0 R >>");

        await Assert.That(Failures(none, PdfALevel.PdfA2B).Contains(AppearancePresent)).IsTrue();
        await Assert.That(Failures(extra, PdfALevel.PdfA2B).Contains(OnlyNormal)).IsTrue();
        await Assert.That(Failures(normal, PdfALevel.PdfA2B)).IsEmpty();
    }

    /// <summary>A button's normal appearance must be a subdictionary holding at least one stream.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ButtonAppearanceSubdictionary()
    {
        var direct = ConformancePdf.Page();
        var stream = direct.AddStream(string.Empty, string.Empty);
        AddWidget(direct, $"/FT /Btn /T (b) /AP << /N {stream} 0 R >>");
        var states = ConformancePdf.Page();
        stream = states.AddStream(string.Empty, string.Empty);
        AddWidget(states, $"/FT /Btn /T (b) /AP << /N << /On {stream} 0 R /Off (x) >> >>");
        var empty = ConformancePdf.Page();
        AddWidget(empty, "/FT /Btn /T (b) /AP << /N << >> >>");

        await Assert.That(Failures(direct, PdfALevel.PdfA2B).Contains(ButtonSubdictionary)).IsTrue();
        await Assert.That(Failures(states, PdfALevel.PdfA2B).Contains(ButtonSubdictionary)).IsFalse();
        await Assert.That(Failures(empty, PdfALevel.PdfA2B).Contains(ButtonSubdictionary)).IsTrue();
    }

    /// <summary>Widgets may not carry actions, and a form field may not carry additional actions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WidgetAndFieldActions()
    {
        var pdf = ConformancePdf.Page();
        var stream = pdf.AddStream(string.Empty, string.Empty);
        AddWidget(pdf, $"/FT /Tx /T (f) /AP << /N {stream} 0 R >> /AA << /E {GoToAction} >>");

        var failures = Failures(pdf, PdfALevel.PdfA2B);

        await Assert.That(failures.Contains(WidgetActions)).IsTrue();
        await Assert.That(failures.Contains(FieldActions)).IsTrue();
    }

    /// <summary>A widget kid without a partial name is not a form field, so its additional actions break only the widget rule.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnnamedKidIsNotAField()
    {
        var pdf = ConformancePdf.Page();
        var stream = pdf.AddStream(string.Empty, string.Empty);
        var widget = pdf.NextNumber;
        var parent = widget + 1;
        _ = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /Parent {parent} 0 R /Rect [0 0 10 10] /F 4 /AP << /N {stream} 0 R >> /AA << /E {GoToAction} >> >>");
        _ = pdf.AddObject($"<< /FT /Tx /T (f) /Kids [{widget} 0 R] >>");
        pdf.PageEntries = $"/Annots [{widget} 0 R]";
        pdf.CatalogEntries = $"/AcroForm << /Fields [{parent} 0 R] >>";

        var failures = Failures(pdf, PdfALevel.PdfA2B);

        await Assert.That(failures.Contains(WidgetActions)).IsTrue();
        await Assert.That(failures.Contains(FieldActions)).IsFalse();
    }

    /// <summary>Forbidden actions fail wherever they are reached: the open action, its chain and outline items.</summary>
    /// <param name="catalogEntries">The catalog entries holding the action.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments($"/OpenAction {LaunchAction}")]
    [Arguments($"/OpenAction << /S /GoTo /D [3 0 R /Fit] /Next [{LaunchAction}] >>")]
    [Arguments($"/Outlines << /First << /Title (t) /A {LaunchAction} >> >>")]
    public async Task ForbiddenActions(string catalogEntries)
    {
        var pdf = ConformancePdf.Page();
        pdf.CatalogEntries = catalogEntries;

        await Assert.That(Failures(pdf, PdfALevel.PdfA2B).Contains(ActionType)).IsTrue();
    }

    /// <summary>PDF/A-4 allows JavaScript actions, which PDF/A-2 forbids.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JavaScriptAllowedInPdfA4()
    {
        var pdf = ConformancePdf.Page();
        pdf.CatalogEntries = "/OpenAction << /S /JavaScript /JS (1) >>";

        await Assert.That(Failures(pdf, PdfALevel.PdfA2B).Contains(ActionType)).IsTrue();
        await Assert.That(Failures(pdf, PdfALevel.PdfA4).Contains(ActionType)).IsFalse();
    }

    /// <summary>Only the four page-turning named actions are allowed.</summary>
    /// <param name="name">The named action.</param>
    /// <param name="fails">Whether the rule fails.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Print", true)]
    [Arguments("NextPage", false)]
    public async Task NamedActions(string name, bool fails)
    {
        var pdf = ConformancePdf.Page();
        pdf.CatalogEntries = $"/OpenAction << /S /Named /N /{name} >>";

        await Assert.That(Failures(pdf, PdfALevel.PdfA2B).Contains(NamedAction)).IsEqualTo(fails);
    }

    /// <summary>PDF/A-4 limits page and annotation triggers but not widget triggers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfA4Triggers()
    {
        var page = ConformancePdf.Page();
        page.PageEntries = $"/AA << /O {GoToAction} >>";
        var widget = ConformancePdf.Page();
        var stream = widget.AddStream(string.Empty, string.Empty);
        AddWidget(widget, $"/FT /Tx /T (f) /AP << /N {stream} 0 R >> /AA << /K {GoToAction} >>");

        await Assert.That(Failures(page, PdfALevel.PdfA4).Contains(Triggers)).IsTrue();
        await Assert.That(Failures(widget, PdfALevel.PdfA4).Contains(Triggers)).IsFalse();
    }

    /// <summary>Optional content configurations need unique names, no AS key and an order listing every group.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptionalContentConfigurations()
    {
        await Assert.That(OptionalContentFailures("/D << /Name () >>").Contains(ConfigName)).IsTrue();
        await Assert.That(OptionalContentFailures("/D << /Name (a) >> /Configs [<< /Name (a) >>]").Contains(UniqueName)).IsTrue();
        await Assert.That(OptionalContentFailures("/D << /Name (a) /Order [{0} 0 R] >>").Contains(OrderListsGroups)).IsTrue();
        await Assert.That(OptionalContentFailures("/D << /Name (a) /Order [{0} 0 R [(label) {1} 0 R]] >>")).IsEmpty();
        await Assert.That(OptionalContentFailures("/D << /Name (a) /AS [] >>").Contains(AutoState)).IsTrue();
    }

    /// <summary>The report lists the rules this area checks, with their clauses, among the checked rules.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RulesAreReportedAsChecked()
    {
        using var document = PdfDocumentReader.Open(ConformancePdf.Page().ToBytes(), null);

        var report = PdfAValidator.Validate(document, PdfALevel.PdfA2B);
        var checkedRules = report.Checked.Select(static rule => $"{rule.Clause}:{rule.Test}").ToHashSet(StringComparer.Ordinal);

        await Assert.That(checkedRules.Contains(AnnotationType)).IsTrue();
        await Assert.That(checkedRules.Contains(ActionType)).IsTrue();
        await Assert.That(checkedRules.Contains(OrderListsGroups)).IsTrue();
    }

    /// <summary>Adds an annotation to the page.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="entries">The annotation dictionary's entries.</param>
    private static void AddAnnotation(RenderTestPdf pdf, string entries)
    {
        var number = pdf.AddObject($"<< {entries} >>");
        pdf.PageEntries = $"/Annots [{number} 0 R]";
    }

    /// <summary>Adds a widget annotation that is also a form field.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="entries">The field and widget entries besides the type, rectangle and flags.</param>
    private static void AddWidget(RenderTestPdf pdf, string entries)
    {
        var number = pdf.AddObject($"<< /Type /Annot /Subtype /Widget /Rect [0 0 10 10] /F 4 {entries} >>");
        pdf.PageEntries = $"/Annots [{number} 0 R]";
        pdf.CatalogEntries = $"/AcroForm << /Fields [{number} 0 R] >>";
    }

    /// <summary>Validates a page with two optional content groups and the given configurations.</summary>
    /// <param name="configurations">The <c>/D</c> and <c>/Configs</c> entries; {0} and {1} stand for the groups' numbers.</param>
    /// <returns>The failed rules.</returns>
    private static HashSet<string> OptionalContentFailures(string configurations)
    {
        var pdf = ConformancePdf.Page();
        var first = pdf.AddObject("<< /Type /OCG /Name (g) >>");
        var second = pdf.AddObject("<< /Type /OCG /Name (h) >>");
        var entries = configurations.Replace("{0}", first.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{1}", second.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
        pdf.CatalogEntries = $"/OCProperties << /OCGs [{first} 0 R {second} 0 R] {entries} >>";
        return Failures(pdf, PdfALevel.PdfA2B, "6.9:");
    }

    /// <summary>Validates a PDF and lists the failed rules as clause:test.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="level">The level.</param>
    /// <returns>The failed rules.</returns>
    private static HashSet<string> Failures(RenderTestPdf pdf, PdfALevel level) => Failures(pdf, level, string.Empty);

    /// <summary>Validates a PDF and lists the failed rules whose ids start with a prefix.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="level">The level.</param>
    /// <param name="prefix">The clause prefix to keep, or empty for the rules of this area.</param>
    /// <returns>The failed rules.</returns>
    private static HashSet<string> Failures(RenderTestPdf pdf, PdfALevel level, string prefix)
    {
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        var report = PdfAValidator.Validate(document, level);
        return report.Failed
            .Select(static rule => $"{rule.Clause}:{rule.Test}")
            .Where(id => prefix.Length == 0 ? IsInteractiveRule(id) : id.StartsWith(prefix, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>Determines whether a rule id belongs to the areas these tests cover, leaving out metadata and file structure rules.</summary>
    /// <param name="id">The clause:test id.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsInteractiveRule(string id) =>
        id.StartsWith("6.3.", StringComparison.Ordinal) || id.StartsWith("6.4.", StringComparison.Ordinal)
        || id.StartsWith("6.5.", StringComparison.Ordinal) || id.StartsWith("6.6.1", StringComparison.Ordinal)
        || id.StartsWith("6.6.3", StringComparison.Ordinal) || id.StartsWith("6.9:", StringComparison.Ordinal);
}
