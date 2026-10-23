// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Forms;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Links, form fields and annotations read through the engine's caches are the same after every kind of edit as a fresh read.</summary>
public sealed class ReadCacheAfterEditTests
{
    /// <summary>The note's colour.</summary>
    private const uint NoteColor = 0xFFEE88;

    /// <summary>The note's position.</summary>
    private const float NoteAt = 40;

    /// <summary>The text typed into a field.</summary>
    private const string Typed = "typed";

    /// <summary>The name of the sample form's text field.</summary>
    private const string FieldName = "Name";

    /// <summary>The reads and edits per thread in the concurrency test.</summary>
    private const int Rounds = 200;

    /// <summary>The reads per reader thread in the concurrency test.</summary>
    private const int ReadRounds = 1000;

    /// <summary>The reader threads in the concurrency test.</summary>
    private const int Readers = 4;

    /// <summary>The note text.</summary>
    private const string NoteText = "A note";

    /// <summary>The text a note is changed to.</summary>
    private const string EditedText = "Edited";

    /// <summary>The left and top of the moved note.</summary>
    private const float MovedNear = 50;

    /// <summary>The right and bottom of the moved note.</summary>
    private const float MovedFar = 70;

    /// <summary>A field edit keeps the page's links and an unedited page's list is the same instance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldEditKeepsTheCachedLinks()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateForm());
        var before = HyperPdfNavigation.GetLinks(test.Document, 0);
        var field = Field(test);

        var changed = HyperPdfDocumentFormFilling.SetText(test.Document, 0, field.Index, Typed);

        await Assert.That(changed).IsTrue();
        await Assert.That(ReferenceEquals(HyperPdfNavigation.GetLinks(test.Document, 0), before)).IsTrue();
    }

    /// <summary>An annotation edit on the page reads the page's links again and finds the same links.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationEditReadsThePageLinksAgain()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateWithFileLinks());
        var before = HyperPdfNavigation.GetLinks(test.Document, 0);

        var index = HyperPdfDocumentAnnotationEditing.AddNote(test.Document, 0, new(NoteAt, NoteAt), NoteText, NoteColor);
        var afterAdd = HyperPdfNavigation.GetLinks(test.Document, 0);
        var contents = HyperPdfDocumentAnnotationEditing.SetContents(test.Document, 0, index, EditedText);
        var afterContents = HyperPdfNavigation.GetLinks(test.Document, 0);
        var removed = HyperPdfDocumentAnnotationEditing.Remove(test.Document, 0, index);
        var afterRemove = HyperPdfNavigation.GetLinks(test.Document, 0);

        await Assert.That(before.Count).IsGreaterThan(0);
        await Assert.That(index).IsGreaterThanOrEqualTo(0);
        await Assert.That(contents).IsTrue();
        await Assert.That(removed).IsTrue();
        await Assert.That(ReferenceEquals(afterAdd, before)).IsFalse();
        await Assert.That(afterAdd.SequenceEqual(before)).IsTrue();
        await Assert.That(afterContents.SequenceEqual(before)).IsTrue();
        await Assert.That(afterRemove.SequenceEqual(before)).IsTrue();
    }

    /// <summary>An edit that names no page drops the links of every page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnscopedEditDropsTheLinks()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateWithFileLinks());
        var before = HyperPdfNavigation.GetLinks(test.Document, 0);

        HyperPdfEditing.Edited(test.Document);
        var after = HyperPdfNavigation.GetLinks(test.Document, 0);
        HyperPdfEditing.PartsEdited(test.Document);
        var afterParts = HyperPdfNavigation.GetLinks(test.Document, 0);

        await Assert.That(ReferenceEquals(after, before)).IsFalse();
        await Assert.That(ReferenceEquals(afterParts, after)).IsFalse();
        await Assert.That(after.SequenceEqual(before)).IsTrue();
    }

    /// <summary>Cached form fields are reused until an edit, then show the edited value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldsAreReusedUntilAnEditThenShowTheNewValue()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateForm());
        var first = Field(test);
        var again = Field(test);

        var changed = HyperPdfDocumentFormFilling.SetText(test.Document, 0, first.Index, Typed);
        var edited = Field(test);

        await Assert.That(ReferenceEquals(first, again)).IsTrue();
        await Assert.That(changed).IsTrue();
        await Assert.That(edited.Value).IsEqualTo(Typed);
        await Assert.That(edited.Bounds).IsEqualTo(first.Bounds);
        await Assert.That(first.Value).IsNotEqualTo(Typed);
    }

    /// <summary>Check box state read through the cache follows each change.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CheckBoxStateFollowsEachChange()
    {
        using var test = new HyperPdfTestDocument(TestPdf.CreateForm());
        var box = Fields(test).First(static f => f.Kind == FormFieldKind.CheckBox);

        _ = HyperPdfDocumentFormFilling.SetChecked(test.Document, 0, box.Index, !box.IsChecked);
        var toggled = Fields(test).First(f => f.Index == box.Index);
        _ = HyperPdfDocumentFormFilling.SetChecked(test.Document, 0, box.Index, box.IsChecked);
        var restored = Fields(test).First(f => f.Index == box.Index);

        await Assert.That(toggled.IsChecked).IsEqualTo(!box.IsChecked);
        await Assert.That(restored.IsChecked).IsEqualTo(box.IsChecked);
    }

    /// <summary>Annotations read after an add, a contents change and a removal match each change, and a warm read is not copied.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsFollowAddContentsAndRemove()
    {
        using var test = new HyperPdfTestDocument(TestPdf.Create(1));
        var empty = Read(test).Length;

        var index = HyperPdfDocumentAnnotationEditing.AddNote(test.Document, 0, new(NoteAt, NoteAt), NoteText, NoteColor);
        var added = Read(test);
        var warm = HyperPdfAnnotationReading.GetAnnotations(test.Editor, 0);
        var warmAgain = HyperPdfAnnotationReading.GetAnnotations(test.Editor, 0);
        _ = HyperPdfDocumentAnnotationEditing.SetContents(test.Document, 0, index, EditedText);
        var edited = Read(test);
        _ = HyperPdfDocumentAnnotationEditing.SetBounds(test.Document, 0, index, new(MovedNear, MovedNear, MovedFar, MovedFar));
        var moved = Read(test);
        _ = HyperPdfDocumentAnnotationEditing.Remove(test.Document, 0, index);
        var removed = Read(test);

        await Assert.That(empty).IsEqualTo(0);
        await Assert.That(added.Length).IsEqualTo(1);
        await Assert.That(warm.Equals(warmAgain)).IsTrue();
        await Assert.That(edited[0].Contents).IsEqualTo(EditedText);
        await Assert.That(moved[0].Bounds).IsNotEqualTo(edited[0].Bounds);
        await Assert.That(removed.Length).IsEqualTo(0);
    }

    /// <summary>Readers racing an editing thread always see a whole snapshot and the final state once the editor stops.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadersRacingAnEditorSeeWholeSnapshots()
    {
        using var test = new HyperPdfTestDocument(TestPdf.Create(1));
        var torn = 0;
        var editor = Task.Run(() =>
        {
            for (var i = 0; i < Rounds; i++)
            {
                var index = HyperPdfDocumentAnnotationEditing.AddNote(test.Document, 0, new(NoteAt, NoteAt), NoteText, NoteColor);
                _ = HyperPdfDocumentAnnotationEditing.Remove(test.Document, 0, index);
            }
        });
        var readers = Enumerable.Range(0, Readers).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < ReadRounds; i++)
            {
                var annotations = HyperPdfAnnotationReading.GetAnnotations(test.Editor, 0).Span;
                if (annotations.Length > 1 || (annotations.Length == 1 && annotations[0].Contents != NoteText))
                {
                    _ = Interlocked.Increment(ref torn);
                }
            }
        })).ToArray();

        await editor;
        await Task.WhenAll(readers);

        await Assert.That(torn).IsEqualTo(0);
        await Assert.That(Read(test).Length).IsEqualTo(0);
    }

    /// <summary>Reads a page's annotations into an array.</summary>
    /// <param name="test">The document.</param>
    /// <returns>The annotations.</returns>
    private static PageAnnotation[] Read(HyperPdfTestDocument test) => HyperPdfAnnotationReading.GetAnnotations(test.Editor, 0).ToArray();

    /// <summary>Reads the first page's form fields.</summary>
    /// <param name="test">The document.</param>
    /// <returns>The fields.</returns>
    private static List<FormField> Fields(HyperPdfTestDocument test)
    {
        List<FormField> fields = [];
        HyperPdfDocumentFormFilling.GetFields(test.Document, 0, fields);
        return fields;
    }

    /// <summary>Reads the sample form's text field.</summary>
    /// <param name="test">The document.</param>
    /// <returns>The field.</returns>
    private static FormField Field(HyperPdfTestDocument test) => Fields(test).First(static f => f.Name == FieldName);
}
