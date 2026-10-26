// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Finding;
using HyperPdf.ViewModels.Markup;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Language refresh preserves tag edits and folder search inputs.</summary>
[NotInParallel]
[Category("LiveCulture")]
public sealed class TagsAndFolderCultureTests
{
    /// <summary>Observable choice lists support the read-only IList contract required by UI item sources.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ObservableChoicesExposeReadOnlyListContract()
    {
        await Assert.That(AnnotationsViewModel.TypeFilterOptions is IList).IsTrue();
        var list = (IList)AnnotationsViewModel.TypeFilterOptions;
        var first = list[0];
        var copied = new object?[list.Count];
        list.CopyTo(copied, 0);
        await Assert.That(list.IsReadOnly).IsTrue();
        await Assert.That(list.IsFixedSize).IsTrue();
        await Assert.That(list.IndexOf(first)).IsEqualTo(0);
        await Assert.That(list.Contains(first)).IsTrue();
        await Assert.That(copied[0]).IsEqualTo(first);
        await Assert.That(() => list.Add(first)).Throws<NotSupportedException>();
        await Assert.That(() => list.Clear()).Throws<NotSupportedException>();
        await Assert.That(() => list.Insert(0, first)).Throws<NotSupportedException>();
        await Assert.That(() => list.Remove(first)).Throws<NotSupportedException>();
        await Assert.That(() => list.RemoveAt(0)).Throws<NotSupportedException>();
        await Assert.That(() => list[0] = first).Throws<NotSupportedException>();
    }

    /// <summary>Selection identities stay fixed while the legacy label list changes language.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoiceResourceIdentitiesSurviveCultureChanges()
    {
        const string desktopKey = "Follow the desktop";
        var previous = UiText.UICulture;
        var previousDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            var labels = PreferencesViewModel.ColorSchemeOptions;
            var identities = LocalisationStringList.GetResourceKeys(labels);
            var count = identities.Count;
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("fr"));
            UiText.RefreshChoices();
            await Assert.That(ReferenceEquals(identities, LocalisationStringList.GetResourceKeys(labels))).IsTrue();
            await Assert.That(identities.Count).IsEqualTo(count);
            await Assert.That(identities[0]).IsEqualTo(desktopKey);
            await Assert.That(labels[0]).IsEqualTo(UiText.Get(desktopKey));
            await Assert.That(identities is System.Collections.Specialized.INotifyCollectionChanged).IsFalse();
            await Assert.That(((IList)identities).IsReadOnly).IsTrue();
            await Assert.That(() => ((IList)identities)[0] = "Changed").Throws<NotSupportedException>();
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
            CultureInfo.DefaultThreadCurrentUICulture = previousDefault;
        }
    }

    /// <summary>An invalid search retains its inputs while its status follows the language.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FolderStatusFollowsCultureWithoutRestartingSearch()
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            using var test = new TestServices();
            using var model = new FolderSearchViewModel(test.Services, static (_, _, _) => { });
            model.Folder = "An unselected document folder";
            model.Query = "Words supplied by the reader";
            await model.SearchAsync();
            var results = model.Results;
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("fr"));
            LocalisationRefresh.Refresh(model);
            await Assert.That(model.Status).IsEqualTo(UiText.Get("Choose a folder and the words to find."));
            await Assert.That(model.Query).IsEqualTo("Words supplied by the reader");
            await Assert.That(model.Folder).IsEqualTo("An unselected document folder");
            await Assert.That(ReferenceEquals(model.Results, results)).IsTrue();
            await Assert.That(model.Results.Count).IsEqualTo(0);
            await Assert.That(model.IsSearching).IsFalse();
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }

    /// <summary>A completed check and status translate without replacing the selected tag or pending edits.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TagMessagesFollowCultureWithoutReplacingEdits()
    {
        var previous = UiText.UICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            using var test = new TestServices();
            using var main = new MainViewModel(test.Services);
            await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("tag-culture.pdf", TestPdf.CreateTagged())]);
            var model = main.SelectedTab!.Tags;
            model.Refresh();
            model.SelectedItem = model.Items[1];
            _ = await model.CheckCommand.Execute().ToTask();
            _ = await model.AddRoleMappingCommand.Execute().ToTask();
            model.Title = "A title still being edited";
            var selected = model.SelectedItem;
            var problems = model.Problems;
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("de"));
            LocalisationRefresh.Refresh(model);
            var summary = problems.Count switch
            {
                0 => UiText.Get("No problems found. A person should still read the document with a screen reader."),
                1 => UiText.Get("1 thing to look at."),
                _ => UiText.Format("{0} things to look at.", problems.Count),
            };
            await Assert.That(model.ProblemSummary).IsEqualTo(summary);
            await Assert.That(model.Status).IsEqualTo(UiText.Get("Type a name for the custom tag and choose the standard tag it stands for."));
            await Assert.That(model.Title).IsEqualTo("A title still being edited");
            await Assert.That(ReferenceEquals(model.SelectedItem, selected)).IsTrue();
            await Assert.That(ReferenceEquals(model.Problems, problems)).IsTrue();
        }
        finally
        {
            InterfaceCulture.Apply(UiText, previous);
        }
    }
}
