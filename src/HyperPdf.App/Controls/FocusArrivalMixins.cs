// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Input;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Controls;

/// <summary>The focus changes of every window, as one stream.</summary>
internal static class FocusArrivalMixins
{
    /// <summary>Adds the focus changes of every window.</summary>
    extension(InputElement)
    {
        /// <summary>
        /// Gets each step of every focus change in every window, including windows opened later. A window's own
        /// <c>Events().GotFocus</c> covers only that window, and the app's windows come and go, so this reads the focus
        /// event's class-wide stream. It is the one place the app does, and the only routed event it reads this way.
        /// </summary>
        internal static IObservable<FocusArrival> FocusArrivals =>
            InputElement.GotFocusEvent.Raised.Select(static raised => new FocusArrival(raised.Item1, raised.Item2));
    }
}
