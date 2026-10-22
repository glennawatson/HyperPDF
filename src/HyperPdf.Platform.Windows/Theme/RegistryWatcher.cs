// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Win32;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Platform.Windows.Theme;

/// <summary>Watches registry keys under HKEY_CURRENT_USER on a background thread until disposed.</summary>
[DebuggerDisplay("RegistryWatcher: {_keys.Length} keys")]
internal sealed class RegistryWatcher : IDisposable
{
    /// <summary>Notify on value changes and new subkeys.</summary>
    private const uint ChangeFilter = 0x1 | 0x4;

    /// <summary>The keys and whether their subkeys are watched too.</summary>
    private readonly (string Path, bool Subtree)[] _keys;

    /// <summary>Signalled to stop.</summary>
    private readonly ManualResetEvent _stop = new(false);

    /// <summary>The changes.</summary>
    private readonly Signal<RxVoid> _changes = new();

    /// <summary>The watching thread.</summary>
    private readonly Thread _thread;

    /// <summary>Initializes a new instance of the <see cref="RegistryWatcher"/> class and starts watching.</summary>
    /// <param name="keys">The keys and whether their subkeys are watched too.</param>
    internal RegistryWatcher((string Path, bool Subtree)[] keys)
    {
        _keys = keys;
        _thread = new(Watch) { IsBackground = true, Name = "Registry changes" };
        _thread.Start();
        Changes = new(_changes);
    }

    /// <summary>Gets the changes, on the watching thread.</summary>
    internal AsObservableSignal<RxVoid> Changes { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _ = _stop.Set();
        _thread.Join();
        _stop.Dispose();
        _changes.Dispose();
    }

    /// <summary>Waits for changes until stopped.</summary>
    private void Watch()
    {
        var keys = new RegistryKey?[_keys.Length];
        using var changed = new AutoResetEvent(false);
        try
        {
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i] = Registry.CurrentUser.OpenSubKey(_keys[i].Path);
            }

            var watching = true;
            while (watching)
            {
                for (var i = 0; i < keys.Length; i++)
                {
                    if (keys[i] is { } key)
                    {
                        _ = NativeMethods.RegNotifyChangeKeyValue(key.Handle, _keys[i].Subtree ? 1 : 0, ChangeFilter, changed.SafeWaitHandle, 1);
                    }
                }

                watching = WaitHandle.WaitAny([_stop, changed]) != 0;
                if (watching)
                {
                    _changes.OnNext(RxVoid.Default);
                }
            }
        }
        finally
        {
            foreach (var key in keys)
            {
                key?.Dispose();
            }
        }
    }
}
