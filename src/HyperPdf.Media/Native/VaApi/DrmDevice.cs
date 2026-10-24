// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Media.Native.VaApi;

/// <summary>Owns the file descriptor of a DRM render node and closes it when disposed, or when the handle is finalized if it never was.</summary>
[DebuggerDisplay("DrmDevice: {handle}")]
internal sealed class DrmDevice : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>The open flag for reading and writing (<c>O_RDWR</c>).</summary>
    private const int ReadWrite = 2;

    /// <summary>The open flag that closes the file in child processes (<c>O_CLOEXEC</c> on Linux).</summary>
    private const int CloseOnExec = 0x80000;

    /// <summary>Initializes a new instance of the <see cref="DrmDevice"/> class that owns nothing.</summary>
    private DrmDevice()
        : base(true)
    {
    }

    /// <summary>Gets the file descriptor.</summary>
    internal int Descriptor => (int)handle;

    /// <summary>Opens a device file.</summary>
    /// <param name="path">The path of the device file.</param>
    /// <returns>The device, or <see langword="null"/> when it could not be opened.</returns>
    internal static DrmDevice? TryOpen(string path)
    {
        var descriptor = NativeMethods.OpenDevice(path, ReadWrite | CloseOnExec);
        if (descriptor < 0)
        {
            return null;
        }

        var device = new DrmDevice();
        device.SetHandle(descriptor);
        return device;
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle() => NativeMethods.CloseDevice((int)handle) == 0;
}
