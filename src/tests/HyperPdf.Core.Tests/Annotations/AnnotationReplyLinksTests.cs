// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;

namespace HyperPdf.Core.Tests.Annotations;

/// <summary>Checks saved comment threads, removals and pending annotation entries.</summary>
public sealed class AnnotationReplyLinksTests
{
    /// <summary>The page object number.</summary>
    private const int PageNumber = 3;

    /// <summary>The reply object number.</summary>
    private const int ReplyNumber = 5;

    /// <summary>The restored annotation object number.</summary>
    private const int RestoredNumber = 7;

    /// <summary>The indirect annotation array object number.</summary>
    private const int ArrayNumber = 8;

    /// <summary>An unused object dictionary in the annotation samples.</summary>
    private const string EmptyDictionary = "<< >>";

    /// <summary>Resolves replies by name and replaces older reply relationship entries.</summary>
    /// <param name="indirectArray">Whether the page references its annotation array.</param>
    /// <param name="inlineParent">Whether the original comment is stored inline.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(true, false)]
    [Arguments(false, true)]
    [Arguments(true, true)]
    public async Task Link_PendingReply_WritesStandardRelationship(bool indirectArray, bool inlineParent)
    {
        const string Parent = "<< /Type /Annot /Subtype /Text /Rect [0 0 10 10] /NM (parent) /Contents (Original) >>";
        var array = $"[{(inlineParent ? Parent : "4 0 R")} 5 0 R]";
        var file = Create(
            indirectArray ? "8 0 R" : array,
            Parent,
            "<< /Type /Annot /Subtype /Text /Rect [0 0 10 10] /NM (reply) /PVLInReplyTo (parent) /IRT 6 0 R /RT /Group >>",
            EmptyDictionary,
            EmptyDictionary,
            array);

        var linked = AnnotationReplyLinks.Link(file);
        var structure = PdfReader.Read(linked);
        var reply = Encoding.Latin1.GetString(PdfReader.GetObject(structure, ReplyNumber).Span);
        var parentReference = inlineParent ? "/IRT 9 0 R" : "/IRT 4 0 R";

        await Assert.That(linked.AsSpan(0, file.Length).SequenceEqual(file)).IsTrue();
        await Assert.That(reply).Contains(parentReference);
        await Assert.That(reply).Contains("/RT /R");
        await Assert.That(reply).DoesNotContain("/PVLInReplyTo");
        await Assert.That(reply).DoesNotContain("/Group");
        await Assert.That(AnnotationReplyLinks.CreateUpdate(linked)).IsEmpty();
        if (!inlineParent)
        {
            return;
        }

        const int AllocatedParentNumber = 9;
        await Assert.That(Encoding.Latin1.GetString(PdfReader.GetObject(structure, AllocatedParentNumber).Span)).Contains("/Contents (Original)");
    }

    /// <summary>Leaves removed comments out of the array and writes restored entries as PDF values.</summary>
    /// <param name="indirectArray">Whether the page references the annotation array.</param>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task Link_RemovedAndRestoredComments_FinishesArrayAndEntries(bool indirectArray)
    {
        const string Array = "[4 0 R 6 0 R 7 0 R]";
        var file = Create(
            indirectArray ? "8 0 R" : Array,
            "<< /Type /Annot /Subtype /Text /Rect [0 0 10 10] /NM (kept) >>",
            EmptyDictionary,
            "<< /Type /Annot /Subtype /Text /Rect [0 0 10 10] /NM (removed) /PVLRemoved (yes) >>",
            "<< /Type /Annot /Subtype /Polygon /Rect [0 0 10 10] /NM (restored) /PVLRemoved () /Vertices [1 1 2 1 2 2] /C [0 0 0] /PVLEntries (/Vertices [0 0 10 0 10 10] /C [1 0 0]) >>",
            Array);

        var linked = AnnotationReplyLinks.Link(file);
        var structure = PdfReader.Read(linked);
        var holder = PdfReader.GetObject(structure, indirectArray ? ArrayNumber : PageNumber);
        var array = indirectArray ? holder : PdfReader.Resolve(structure, holder, PdfSyntax.FindKey(holder.Span, 0, "Annots"u8));
        var restored = Encoding.Latin1.GetString(PdfReader.GetObject(structure, RestoredNumber).Span);

        await Assert.That(Encoding.Latin1.GetString(array.Span)).IsEqualTo("[4 0 R 7 0 R ]");
        await Assert.That(restored).Contains("/Vertices [0 0 10 0 10 10]");
        await Assert.That(restored).Contains("/C [1 0 0]");
        await Assert.That(restored).DoesNotContain("/PVLRemoved");
        await Assert.That(restored).DoesNotContain("/PVLEntries");
        await Assert.That(restored).DoesNotContain("/Vertices [1 1 2 1 2 2]");
        await Assert.That(AnnotationReplyLinks.CreateUpdate(linked)).IsEmpty();
    }

    /// <summary>Preserves an unresolved reply rather than inventing an object reference.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Link_UnknownParent_DoesNotInventReference()
    {
        var file = Create(
            "[5 0 R]",
            EmptyDictionary,
            "<< /Type /Annot /Subtype /Text /Rect [0 0 10 10] /PVLInReplyTo (missing) >>",
            EmptyDictionary,
            EmptyDictionary,
            "[]");

        var linked = AnnotationReplyLinks.Link(file);
        var reply = Encoding.Latin1.GetString(PdfReader.GetObject(PdfReader.Read(linked), ReplyNumber).Span);

        await Assert.That(reply).Contains("/PVLInReplyTo (missing)");
        await Assert.That(reply).DoesNotContain("/IRT");
    }

    /// <summary>Does not rewrite unrelated occurrences of private key names in comment text.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Link_NoPendingDictionaryEntries_ReturnsOriginal()
    {
        var file = Create(
            "[4 0 R]",
            "<< /Type /Annot /Subtype /Text /Rect [0 0 10 10] /Contents (PVLInReplyTo PVLRemoved PVLEntries) >>",
            EmptyDictionary,
            EmptyDictionary,
            EmptyDictionary,
            "[]");

        await Assert.That(AnnotationReplyLinks.Link(file)).IsSameReferenceAs(file);
        await Assert.That(AnnotationReplyLinks.CreateUpdate(file)).IsEmpty();
    }

    /// <summary>Private editing keys outside annotations do not create an annotation array.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Link_PageWithoutAnnotations_ReturnsOriginal()
    {
        var file = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /PVLInReplyTo (parent) >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> >>");

        await Assert.That(AnnotationReplyLinks.Link(file)).IsSameReferenceAs(file);
        await Assert.That(AnnotationReplyLinks.CreateUpdate(file)).IsEmpty();
    }

    /// <summary>Files without pending entries need no parsing or incremental update.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Link_NoPendingWork_ReturnsOriginal()
    {
        var file = TestPdf.Create(1);

        await Assert.That(AnnotationReplyLinks.HasPendingWork(file)).IsFalse();
        await Assert.That(AnnotationReplyLinks.Link(file)).IsSameReferenceAs(file);
        await Assert.That(AnnotationReplyLinks.CreateUpdate(file)).IsEmpty();
    }

    /// <summary>Unreadable pending files are left alone instead of producing a partial update.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task Link_UnreadablePendingFile_ReturnsOriginal()
    {
        var file = "not a PDF /PVLInReplyTo (parent)"u8.ToArray();

        await Assert.That(AnnotationReplyLinks.HasPendingWork(file)).IsTrue();
        await Assert.That(AnnotationReplyLinks.Link(file)).IsSameReferenceAs(file);
        await Assert.That(static () => AnnotationReplyLinks.Link(null!)).Throws<ArgumentNullException>();
    }

    /// <summary>Exposes the editing keys used by saved annotations.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PrivateKeys_MatchSavedNames()
    {
        await Assert.That(AnnotationReplyLinks.PendingKey).IsEqualTo("PVLInReplyTo");
        await Assert.That(AnnotationReplyLinks.RemovedKey).IsEqualTo("PVLRemoved");
        await Assert.That(AnnotationReplyLinks.EntriesKey).IsEqualTo("PVLEntries");
    }

    /// <summary>Creates a single-page file with fixed annotation object numbers.</summary>
    /// <param name="annots">The page's annotation array or reference.</param>
    /// <param name="parent">The original comment dictionary.</param>
    /// <param name="reply">The reply dictionary.</param>
    /// <param name="removed">The removed annotation dictionary.</param>
    /// <param name="restored">The restored annotation dictionary.</param>
    /// <param name="array">The referenced annotation array.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Create(string annots, string parent, string reply, string removed, string restored, string array) =>
        MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << >> /Annots {annots} >>",
            parent,
            reply,
            removed,
            restored,
            array);
}
