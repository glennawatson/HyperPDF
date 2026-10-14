// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections;
using System.Collections.Specialized;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.ObservableEvents;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.ViewModels;

/// <summary>Streams of the lists view models share with views.</summary>
internal static class CollectionMixins
{
    /// <summary>Adds the edit stream of a list.</summary>
    /// <param name="list">The list.</param>
    extension(IList list)
    {
        /// <summary>Gets the list now, and again each time it is changed in place when it reports its changes.</summary>
        internal IObservable<IList> Edits => list is INotifyCollectionChanged collection
            ? collection.Events().CollectionChanged.SelectWith(list, static (edited, _) => edited).StartWith(list)
            : Signal.Return(list);
    }
}
