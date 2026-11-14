// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Viewer;

/// <summary>The command a menu copies text with: it puts its parameter on the clipboard of a window.</summary>
/// <param name="window">The window.</param>
[DebuggerDisplay("CopyTextCommand")]
internal sealed class CopyTextCommand(IPlatformWindow window) : ICommand
{
    /// <inheritdoc/>
    public event EventHandler? CanExecuteChanged
    {
        add
        {
            // The command can always run with text, so there is never a change to tell anyone about.
        }

        remove
        {
            // Nothing was kept when the handler was added.
        }
    }

    /// <inheritdoc/>
    public bool CanExecute(object? parameter) => parameter is string { Length: > 0 };

    /// <inheritdoc/>
    public void Execute(object? parameter)
    {
        if (parameter is string { Length: > 0 } text)
        {
            window.SetClipboardText(text);
        }
    }
}
