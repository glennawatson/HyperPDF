// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Conformance.VeraPdf;

/// <summary>
/// Compares the parent-child rules HyperPdf checks with veraPDF's own tagged PDF profiles (ISO 32000-1 and 32000-2 Annex L, and
/// ISO/TS 32005 Table 5). For each parent type named by a rule, one small file holds a child of every type the profile mentions and
/// a second holds each limited child twice; on every file, each rule HyperPdf checks must fail in HyperPdf exactly when it fails in
/// veraPDF. Rules HyperPdf does not check are not compared. The profiles are veraPDF's profile files (CC BY 4.0), which the tests
/// read from the folder named by <see cref="VeraPdfValidator.ProfilesVariable"/>.
/// </summary>
[NotInParallel(nameof(VeraPdfValidator))]
public sealed class StructureContainmentAgreementTests
{
    /// <summary>The structure tree root's name in the profiles, which the files do not vary.</summary>
    private const string TreeRoot = "StructTreeRoot";

    /// <summary>The first object number of a child in a file.</summary>
    private const int FirstChildObject = 8;

    /// <summary>The fewest rules that must be compared, so a profile with nothing to check cannot pass.</summary>
    private const int MinimumRules = 100;

    /// <summary>Every rule HyperPdf checks fails or passes as it does in veraPDF on files that break one parent type at a time.</summary>
    /// <param name="profile">The HyperPdf profile.</param>
    /// <param name="profileFile">The name of veraPDF's profile file.</param>
    /// <param name="legacy">Whether the files are PDF 1.7 files that use only the PDF 1.7 namespace.</param>
    /// <param name="cancellationToken">Cancels the checks.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfStandardsProfile.Iso32005, "ISO-32005-Tagged.xml", false)]
    [Arguments(PdfStandardsProfile.TaggedIso32000Part1, "ISO-32000-1-Tagged.xml", false)]
    [Arguments(PdfStandardsProfile.TaggedIso32000Part2, "ISO-32000-2-Tagged.xml", false)]
    [Arguments(PdfStandardsProfile.Iso32005, "ISO-32005-Tagged.xml", true)]
    [Arguments(PdfStandardsProfile.TaggedIso32000Part1, "ISO-32000-1-Tagged.xml", true)]
    [Arguments(PdfStandardsProfile.TaggedIso32000Part2, "ISO-32000-2-Tagged.xml", true)]
    public async Task PairRulesAgreeWithVeraPdf(PdfStandardsProfile profile, string profileFile, bool legacy, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        var profilePath = VeraPdfValidator.RequireProfile(profileFile);
        var parents = ReadParents(profile);
        using var folder = new PdfAOutputFolder("containment-agreement");
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (parent, children) in parents)
        {
            await Write(folder.Path, files, $"{parent}-once", parent, children.Once, legacy, cancellationToken);
            await Write(folder.Path, files, $"{parent}-twice", parent, children.Twice, legacy, cancellationToken);
        }

        var jobs = await VeraPdfValidator.ValidateProfileAsync(home, profilePath, [.. files.Keys], cancellationToken);
        var disagreements = new List<string>();
        var compared = 0;
        foreach (var path in files.Keys)
        {
            using var document = await PdfDocumentReader.OpenAsync(path, null, cancellationToken);
            var report = await PdfDocumentStandards.CheckAsync(document, profile, cancellationToken);
            compared += Compare(files[path], report, jobs[Path.GetFullPath(path)], disagreements);
        }

        await Assert.That(compared).IsGreaterThan(MinimumRules);
        await Assert.That(disagreements).IsEmpty().Because($"{disagreements.Count} disagreements: {string.Join(" | ", disagreements)}");
    }

    /// <summary>Compares each pair rule HyperPdf checks with veraPDF's result for one file.</summary>
    /// <param name="name">The file's name, for messages.</param>
    /// <param name="report">HyperPdf's report.</param>
    /// <param name="job">veraPDF's result.</param>
    /// <param name="disagreements">Receives each rule that differs.</param>
    /// <returns>The number of rules compared.</returns>
    private static int Compare(string name, PdfStandardsRuleReport report, VeraPdfJob job, List<string> disagreements)
    {
        var compared = 0;
        foreach (var result in report.Results)
        {
            if (result.Status is PdfStandardsRuleStatus.NotChecked or PdfStandardsRuleStatus.NeedsHumanReview || !IsPairRule(result.Rule))
            {
                continue;
            }

            compared++;
            var theirs = job.Failures.Exists(failure => failure.Clause == result.Rule.Clause && failure.TestNumber == result.Rule.TestNumber);
            if (theirs != (result.Status == PdfStandardsRuleStatus.Failed))
            {
                disagreements.Add($"{name}: {result.Rule.Clause}:{result.Rule.TestNumber} HyperPdf {result.Status}, veraPDF {(theirs ? "fails" : "passes")}");
            }
        }

        return compared;
    }

    /// <summary>Determines whether a rule is a parent-child rule.</summary>
    /// <param name="rule">The rule.</param>
    /// <returns><see langword="true"/> when its text states a parent and a child.</returns>
    private static bool IsPairRule(PdfStandardsRule rule) => StandardsContainmentRules.TryParse(rule.Description, out _);

    /// <summary>Reads the children a profile's rules name for each parent type.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The children of each parent, in the order the rules list them.</returns>
    private static Dictionary<string, ChildSet> ReadParents(PdfStandardsProfile profile)
    {
        var parents = new Dictionary<string, ChildSet>(StringComparer.Ordinal);
        foreach (var rule in PdfDocumentStandards.GetRules(profile))
        {
            if (!StandardsContainmentRules.TryParse(rule.Description, out var pair) || pair.Parent == TreeRoot)
            {
                continue;
            }

            ref var set = ref CollectionsMarshal.GetValueRefOrAddDefault(parents, pair.Parent, out _);
            set ??= new();
            set.Add(pair.Child, pair.Kind == StandardsContainmentKind.AtMostOnce);
        }

        return parents;
    }

    /// <summary>Writes a file and remembers its name.</summary>
    /// <param name="folder">The folder.</param>
    /// <param name="files">Receives the path and the name of the file.</param>
    /// <param name="name">The file's name.</param>
    /// <param name="parent">The parent type.</param>
    /// <param name="children">The children to put in the parent.</param>
    /// <param name="legacy">Whether the file is a PDF 1.7 file that uses only the PDF 1.7 namespace.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    /// <returns>A task.</returns>
    private static async Task Write(string folder, Dictionary<string, string> files, string name, string parent, List<string> children, bool legacy, CancellationToken cancellationToken)
    {
        var path = Path.Combine(folder, $"{name}{(legacy ? "-legacy" : string.Empty)}.pdf");
        await File.WriteAllBytesAsync(path, Build(parent, children, legacy), cancellationToken);
        files.Add(path, name);
    }

    /// <summary>Gives the spelling a file uses for a type name: the profiles write <c>Hn</c> for any numbered heading.</summary>
    /// <param name="type">The profile's name for the type.</param>
    /// <returns>The name for the file.</returns>
    private static string Spell(string type) => type == "Hn" ? "H1" : type;

    /// <summary>
    /// Gives the namespace entry of a type: the PDF 2.0 namespace for a type it defines, and none (the PDF 1.7 namespace) for the rest,
    /// as a real file spells types such as <c>TOCI</c> or <c>Quote</c>, which the PDF 2.0 namespace does not define.
    /// </summary>
    /// <param name="type">The profile's name for the type.</param>
    /// <param name="legacy">Whether the file uses only the PDF 1.7 namespace.</param>
    /// <returns>The entry for the structure element.</returns>
    private static string Namespace(string type, bool legacy) =>
        !legacy && PdfStructureTypes.GetStandardNames(PdfStructureTypes.Pdf20Namespace).Contains(Spell(type), StringComparer.Ordinal) ? "/NS 7 0 R" : string.Empty;

    /// <summary>Builds a file whose Document holds one parent that holds the given children.</summary>
    /// <param name="parent">The parent type.</param>
    /// <param name="children">The child types, each a leaf with one marked-content item.</param>
    /// <param name="legacy">Whether the file is a PDF 1.7 file that uses only the PDF 1.7 namespace, rather than a PDF 2.0 file.</param>
    /// <returns>The file.</returns>
    private static byte[] Build(string parent, List<string> children, bool legacy)
    {
        var references = new List<string>();
        for (var index = 0; index < children.Count; index++)
        {
            references.Add(string.Create(CultureInfo.InvariantCulture, $"{FirstChildObject + index} 0 R"));
        }

        var objects = new List<string>
        {
            $"<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> /Lang (en-US) {(legacy ? string.Empty : "/Version /2.0")} >>",
            "<< /Type /Pages /Count 1 /Kids [3 0 R] >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>",
            $"<< /Type /StructElem /S /Document {Namespace("Document", legacy)} /P 5 0 R /K [6 0 R] >>",
            $"<< /Type /StructTreeRoot /K 4 0 R {(legacy ? string.Empty : "/Namespaces [7 0 R]")} >>",
            $"<< /Type /StructElem /S /{Spell(parent)} {Namespace(parent, legacy)} /P 4 0 R /Pg 3 0 R /K [{string.Join(' ', references)}] >>",
            "<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>",
        };
        foreach (var child in children)
        {
            objects.Add($"<< /Type /StructElem /S /{Spell(child)} {Namespace(child, legacy)} /P 6 0 R /Pg 3 0 R /K 0 >>");
        }

        return MiniPdf.Build([.. objects]);
    }

    /// <summary>The children a profile names for one parent.</summary>
    private sealed class ChildSet
    {
        /// <summary>Gets the children to put in the parent once each: every child a rule names.</summary>
        internal List<string> Once { get; } = [];

        /// <summary>Gets the children to put in the parent twice each: those limited to one.</summary>
        internal List<string> Twice { get; } = [];

        /// <summary>Adds a child a rule names.</summary>
        /// <param name="child">The child type.</param>
        /// <param name="limitedToOne">Whether the rule limits the parent to one such child.</param>
        internal void Add(string child, bool limitedToOne)
        {
            if (!Once.Contains(child))
            {
                Once.Add(child);
            }

            if (!limitedToOne || Twice.Contains(child))
            {
                return;
            }

            Twice.Add(child);
            Twice.Add(child);
        }
    }
}
