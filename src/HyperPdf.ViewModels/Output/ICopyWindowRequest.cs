// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Output;

/// <summary>The view model of a window a tab opens to save a changed copy, which the tab can close when its document goes away.</summary>
public interface ICopyWindowRequest : IDisposable
{
    /// <summary>Gets the command that stops any run in progress and closes the window.</summary>
    ReactiveCommand<RxVoid, RxVoid> CloseCommand { get; }
}
