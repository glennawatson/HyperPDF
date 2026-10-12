// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Advanced;

namespace HyperPdf.Core.Platform;

/// <summary>Owns the "one running window" claim, receiving documents launched while the app is open.</summary>
public interface ISingleInstance : IDisposable
{
    /// <summary>Gets the requests from other launches to open documents or raise the window, on any thread.</summary>
    AsObservableSignal<OpenRequest> OpenRequests { get; }
}
