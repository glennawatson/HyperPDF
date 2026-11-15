// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Windows.Input;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Extensions;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Picks the presses of one kind of control out of the stream of presses, and runs view model commands from them.</summary>
internal static class ChromePressMixins
{
    /// <summary>Adds the picking to a stream of presses.</summary>
    /// <param name="presses">The presses of the window's controls.</param>
    extension(IObservable<ChromePress> presses)
    {
        /// <summary>Keeps the presses of the controls that do one thing.</summary>
        /// <param name="command">What the controls do.</param>
        /// <returns>The presses of those controls.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IObservable<ChromePress> WhereCommand(ChromeCommand command) => presses.WhereWith(command, static (wanted, press) => press.Command == wanted);

        /// <summary>Runs a command that takes nothing each time one of the controls that do one thing is pressed, when the command can run.</summary>
        /// <param name="command">What the controls do.</param>
        /// <param name="run">The view model command.</param>
        /// <returns>What ends the running.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal IDisposable InvokeCommandOn(ChromeCommand command, ICommand run) => presses.WhereCommand(command).AsSignal().InvokeCommand(run);
    }
}
