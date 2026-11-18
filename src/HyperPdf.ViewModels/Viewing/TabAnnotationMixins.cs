// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Markup;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>
/// What a tab can say about its document's annotations without making the annotation tools. The tools are made on first use,
/// and a tab that only shows a document still draws the annotations the document came with.
/// </summary>
public static class TabAnnotationMixins
{
    /// <summary>Adds the annotation facts of a tab.</summary>
    /// <param name="tab">The tab.</param>
    extension(DocumentTabViewModel tab)
    {
        /// <summary>Gets a value indicating whether the open document can be annotated.</summary>
        public bool CanAnnotate => DocumentFeature<IAnnotationEditor>.Get(tab.TryGetDocument()) is not null;

        /// <summary>Gets the annotation that is picked, or <see langword="null"/> when none is or the tools have not been used.</summary>
        public PageAnnotation? PickedAnnotation => tab.AnnotationsInUse?.Selected;

        /// <summary>Gets whether the tab is being annotated, now and as it changes. It is not until the tools have been used.</summary>
        public IObservable<bool> AnnotatingChanges => tab.WhenChanged(static t => t.AnnotationsInUse)
            .SwitchMap(static marks => marks is null ? Signal.Return(false) : marks.WhenChanged(static m => m.IsAnnotating));

        /// <summary>Gets whether Fill and Sign is on, now and as it changes. It is not until it has been used.</summary>
        public IObservable<bool> FillingChanges => tab.MadeParts.OfType<FillAndSignViewModel>()
            .Take(1)
            .SwitchMap(static filling => filling.WhenChanged(static f => f.IsActive))
            .StartWith(false);

        /// <summary>
        /// Tells whether a page has annotations, asking the document. A tab whose annotation tools have not been used makes
        /// them only when the pointer is over a page that has something to point at.
        /// </summary>
        /// <param name="page">The zero-based page.</param>
        /// <param name="scratch">Room for the page's annotations; it is overwritten.</param>
        /// <returns><see langword="true"/> when the page has at least one annotation.</returns>
        public bool HasAnnotationsOn(int page, List<PageAnnotation> scratch)
        {
            tab.GetPageAnnotations(page, scratch);
            return scratch.Count > 0;
        }

        /// <summary>Lists the annotations of one page, for the badges drawn on it.</summary>
        /// <param name="page">The zero-based page.</param>
        /// <param name="output">Receives the annotations; it is cleared first.</param>
        public void GetPageAnnotations(int page, List<PageAnnotation> output)
        {
            ArgumentNullException.ThrowIfNull(output);
            output.Clear();
            if ((uint)page < (uint)tab.PageCount && DocumentFeature<IAnnotationEditor>.Get(tab.TryGetDocument()) is { } editor)
            {
                editor.GetAnnotations(page, output);
            }
        }
    }
}
