// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Licences;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Existing messages and choices follow the selected language without replacing their edited state.</summary>
[NotInParallel]
[Category("LiveCulture")]
public sealed class LiveLocalisationTests
{
    /// <summary>The title and button resource of the note prompt.</summary>
    private const string AddNoteKey = "Add Note";

    /// <summary>The separation fixture's page and colourant count.</summary>
    private const int SeparationCount = 2;

    /// <summary>The two independent authors in the annotation filter fixture.</summary>
    private const int AnnotationCount = 2;

    /// <summary>The selected document author, whose text must never be translated.</summary>
    private const string FirstAuthor = "Alice";

    /// <summary>Document refresh reaches retained child models without constructing unused tools.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefreshesExistingDocumentChildrenWithoutCreatingUnusedModels()
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            using var test = new TestServices();
            using var main = new MainViewModel(test.Services);
            await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("graph-culture.pdf", 1)]);
            var tab = main.SelectedTab!;
            var before = new HashSet<object>(ReferenceEqualityComparer.Instance);
            LocalisationRefresh.RefreshCore(tab, before);
            await Assert.That(tab.PagesInUse).IsNull();
            await Assert.That(tab.TextRecognitionInUse).IsNull();
            await Assert.That(before.OfType<PagePresentationViewModel>().Any()).IsFalse();
            await Assert.That(before.OfType<TabPrintingViewModel>().Any()).IsFalse();
            object[] children =
            [
                tab.Media,
                tab.Parts,
                tab.Attachments,
                tab.PagePresentation,
                tab.Signatures,
                tab.Forms,
                tab.FillAndSign,
                tab.Printing,
                tab.Notices,
                tab.TextRecognition,
                tab.Outline,
                tab.Pages,
            ];
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("ar"));
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            LocalisationRefresh.RefreshCore(tab, visited);
            foreach (var child in children)
            {
                await Assert.That(visited.Contains(child)).IsTrue();
            }
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>Page status and history captions refresh without replacing the document or selection.</summary>
    /// <param name="cultureName">The language selected on the existing page manager.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("fr")]
    [Arguments("de")]
    [Arguments("zh-Hans")]
    [Arguments("ar")]
    [Arguments("es")]
    [Arguments("pt")]
    public async Task RefreshesPageStatusWithoutChangingSelection(string cultureName)
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            using var test = new TestServices();
            using var main = new MainViewModel(test.Services);
            await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("page-culture.pdf", AnnotationCount)]);
            var tab = main.SelectedTab!;
            var model = tab.Pages;
            model.SelectPages([]);
            var selection = model.SelectedPages;
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo(cultureName));
            LocalisationRefresh.Refresh(tab);
            await Assert.That(model.Status).IsEqualTo(UiText.Get("Select one or more pages."));
            await Assert.That(model.UndoText).IsEqualTo(UiText.Get("Undo"));
            await Assert.That(model.RedoText).IsEqualTo(UiText.Get("Redo"));
            await Assert.That(ReferenceEquals(model.SelectedPages, selection)).IsTrue();
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            model.SelectPages([0, 1]);
            selection = model.SelectedPages;
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo(cultureName));
            LocalisationRefresh.Refresh(tab);
            await Assert.That(model.Status).IsEqualTo(UiText.Format("{0} {1} selected.", AnnotationCount, UiText.Get("pages")));
            await Assert.That(model.SelectedPages).IsEquivalentTo([0, 1]);
            await Assert.That(ReferenceEquals(model.SelectedPages, selection)).IsTrue();
            await Assert.That(ReferenceEquals(main.SelectedTab, tab)).IsTrue();
            await Assert.That(ReferenceEquals(tab.Pages, model)).IsTrue();
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>Signing preview captions reformat retained data without asking the document to preview again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefreshesSigningPreviewWithoutRecheckingTheDocument()
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            var calls = 0;
            using var model = new CertificateSignViewModel(
                [],
                0,
                _ =>
                {
                    calls++;
                    return null;
                },
                string.Empty);
            model.Reason = "A reason supplied by the reader";
            var before = calls;
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("fr"));
            LocalisationRefresh.Refresh(model);
            await Assert.That(model.PreviewText).IsEqualTo(SigningPreviewText.Unavailable);
            await Assert.That(model.Reason).IsEqualTo("A reason supplied by the reader");
            await Assert.That(calls).IsEqualTo(before);
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>Separation captions and fallback names update without navigating or replacing their choice list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefreshesSeparationMetadataWithoutSelectingAnotherPage()
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            using var test = new TestServices();
            using var main = new MainViewModel(test.Services);
            await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("separation-culture.pdf", SeparationCount)]);
            var tab = main.SelectedTab!;
            using var model = new PageSeparationViewModel(tab);
            var group = new SeparationGroup(1, [new(0, string.Empty), new(1, "A document-authored ink")]);
            model.Load(new([group], 0), 0);
            var choices = model.Colorants;
            var selected = model.SelectedIndex;
            var page = tab.CurrentPageIndex;
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("ar"));
            LocalisationRefresh.Refresh(model);
            await Assert.That(model.Description).IsEqualTo(SeparationText.Describe(group, group.Pages[0]));
            await Assert.That(model.Colorants[0]).IsEqualTo(SeparationText.UnnamedColorant);
            await Assert.That(model.Colorants[1]).IsEqualTo("A document-authored ink");
            await Assert.That(ReferenceEquals(model.Colorants, choices)).IsTrue();
            await Assert.That(model.SelectedIndex).IsEqualTo(selected);
            await Assert.That(tab.CurrentPageIndex).IsEqualTo(page);
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>The licence version caption follows culture while retaining the immutable version value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefreshesTheLicenceVersionCaption()
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            using var model = new LicencesViewModel(NoticeDocument.Parse(string.Empty), "2.4.0");
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("de"));
            LocalisationRefresh.Refresh(model);
            await Assert.That(model.VersionHeading).IsEqualTo(UiText.Format("Version {0}", model.Version));
            await Assert.That(model.Version).IsEqualTo("2.4.0");
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>All-author and named-author filters retain their meaning across English, French and Arabic.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PreservesAnnotationAuthorFiltersAcrossCultures()
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            using var test = new TestServices();
            using var main = new MainViewModel(test.Services);
            await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("author-culture.pdf", 1)]);
            var model = main.SelectedTab!.Annotations;
            _ = model.ShowItems();
            model.Items.Add(new(new PageAnnotation(0, 0, AnnotationKind.Note, default, 0, "First note", FirstAuthor), "1", []));
            model.Items.Add(new(new PageAnnotation(0, 1, AnnotationKind.Note, default, 0, "Second note", "Bob"), "1", []));
            model.FilterAuthor = FirstAuthor;
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("fr"));
            LocalisationRefresh.Refresh(model);
            await Assert.That(model.FilterAuthor).IsEqualTo(FirstAuthor);
            await Assert.That(model.VisibleItems.Count).IsEqualTo(1);
            model.FilterAuthor = model.AuthorOptions[0];
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("ar"));
            LocalisationRefresh.Refresh(model);
            await Assert.That(model.FilterAuthor).IsEqualTo(UiText.Get("Everyone"));
            await Assert.That(model.VisibleItems.Count).IsEqualTo(AnnotationCount);
            await Assert.That(model.Items[0].Annotation.Author).IsEqualTo(FirstAuthor);
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>Captions change on an existing prompt without replacing or translating its edited text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RetainsPromptEditsWhileCaptionsChange()
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            var model = new PromptViewModel(new TextPrompt(
                LocalizedMessage.Get(UiText, AddNoteKey),
                LocalizedMessage.Get(UiText, "Note"),
                "A document-authored note",
                LocalizedMessage.Get(UiText, AddNoteKey),
                true));
            model.Text = "User edits that must survive the language change";
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("de"));
            LocalisationRefresh.Refresh(model);
            await Assert.That(model.Title).IsEqualTo(UiText.Get(AddNoteKey));
            await Assert.That(model.Label).IsEqualTo(UiText.Get("Note"));
            await Assert.That(model.AcceptText).IsEqualTo(UiText.Get(AddNoteKey));
            await Assert.That(model.Text).IsEqualTo("User edits that must survive the language change");
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>Status keys and nested format arguments resolve again, while user text remains literal.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RetainsStatusIdentityAndLiteralUserText()
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            using var model = new FlattenCopyViewModel(new FakeFlattenCopyWriter(), "document.pdf");
            var selected = model.SelectedScope;
            model.SetStatusText(LocalizedMessage.Format(UiText, "Could not save: {0}", LocalizedMessage.Get(UiText, "Cancelled. No file was saved.")));
            model.SetError(LocalizedMessage.Literal("Save Copy — document authored text"));
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("fr"));
            LocalisationRefresh.Refresh(model);
            await Assert.That(model.StatusText).IsEqualTo(UiText.Format("Could not save: {0}", UiText.Get("Cancelled. No file was saved.")));
            await Assert.That(model.Error).IsEqualTo("Save Copy — document authored text");
            await Assert.That(ReferenceEquals(model.SelectedScope, selected)).IsTrue();
            await Assert.That(model.SelectedScope!.Name).IsEqualTo(UiText.Get(model.SelectedScope.NameKey));
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>Choice notifications preserve object equality and the selected choice instance.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotifiesExistingChoiceWithoutChangingEquality()
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            using var model = new FlattenCopyViewModel(new FakeFlattenCopyWriter(), "document.pdf");
            var selected = model.SelectedScope!;
            var equivalent = new FlattenScopeChoice(selected.Scope, selected.NameKey, selected.DescriptionKey);
            var notifications = new List<string>();
            using var subscription = selected.WhenChanged(static choice => choice.Name).Skip(1).SubscribeSafe(notifications.Add);
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("ar"));
            LocalisationRefresh.Refresh(selected);
            await Assert.That(notifications.Count).IsEqualTo(1);
            await Assert.That(selected.Equals(equivalent)).IsTrue();
            await Assert.That(selected.GetHashCode()).IsEqualTo(equivalent.GetHashCode());
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }
}
