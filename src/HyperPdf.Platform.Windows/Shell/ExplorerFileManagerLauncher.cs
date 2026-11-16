// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Platform;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Platform.Windows.Shell;

/// <summary>Opens File Explorer at a file's folder with the file selected, as Show in Folder does in other Windows apps.</summary>
[DebuggerDisplay("ExplorerFileManagerLauncher: File Explorer")]
public sealed class ExplorerFileManagerLauncher : IFileManagerLauncher
{
    /// <summary>The apartment-threaded COM model the shell expects.</summary>
    private const uint ApartmentThreaded = 0x2;

    /// <inheritdoc/>
    public IObservable<bool> ShowItem(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        // The shell call needs a single-threaded apartment, so it runs on a thread of its own, started by the subscription.
        return Signal.Create<bool>(observer =>
        {
            var thread = new Thread(() =>
            {
                observer.OnNext(Show(Path.GetFullPath(filePath)));
                observer.OnCompleted();
            }) { IsBackground = true, Name = "Show in Explorer" };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return Scope.Create(static () => { });
        });
    }

    /// <summary>Selects a file in Explorer.</summary>
    /// <param name="filePath">The full path.</param>
    /// <returns><see langword="true"/> when Explorer accepted the request.</returns>
    private static bool Show(string filePath)
    {
        var initialized = NativeMethods.CoInitializeEx(0, ApartmentThreaded);
        var item = NativeMethods.ILCreateFromPath(filePath);
        try
        {
            return item != 0 && NativeMethods.SHOpenFolderAndSelectItems(item, 0, 0, 0) >= 0;
        }
        finally
        {
            if (item != 0)
            {
                NativeMethods.ILFree(item);
            }

            if (initialized >= 0)
            {
                NativeMethods.CoUninitialize();
            }
        }
    }
}
