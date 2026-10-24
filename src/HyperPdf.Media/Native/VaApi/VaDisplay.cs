// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Microsoft.Win32.SafeHandles;

namespace HyperPdf.Media.Native.VaApi;

/// <summary>Owns a VA-API display opened on a DRM render node, and the render node's file, and closes both when disposed.</summary>
[DebuggerDisplay("VaDisplay: {handle}")]
internal sealed class VaDisplay : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>The folder of the DRM device files.</summary>
    private const string DeviceFolder = "/dev/dri";

    /// <summary>The start of the names of render nodes.</summary>
    private const string RenderNodePrefix = "renderD";

    /// <summary>The render node file, kept open for as long as the display is used.</summary>
    private DrmDevice? _device;

    /// <summary>Initializes a new instance of the <see cref="VaDisplay"/> class that owns nothing.</summary>
    private VaDisplay()
        : base(true)
    {
    }

    /// <summary>Gets the display.</summary>
    internal nint Display => handle;

    /// <summary>Opens the first render node whose driver starts.</summary>
    /// <param name="failure">Receives why no display was opened, when none was.</param>
    /// <returns>The display, or <see langword="null"/> when there is no render node or no driver started.</returns>
    internal static unsafe VaDisplay? TryOpen(out DecoderFailure failure)
    {
        failure = new("There is no DRM render node", 0);
        if (!OperatingSystem.IsLinux() || !Directory.Exists(DeviceFolder))
        {
            return null;
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(DeviceFolder, $"{RenderNodePrefix}*").Order(StringComparer.Ordinal))
        {
            var device = DrmDevice.TryOpen(path);
            if (device is null)
            {
                failure = new($"{path} could not be opened", 0);
                continue;
            }

            var display = NativeMethods.GetDisplay(device.Descriptor);
            int major;
            int minor;
            var status = display == 0 ? -1 : NativeMethods.Initialize(display, &major, &minor);
            if (status == 0)
            {
                var opened = new VaDisplay { _device = device };
                opened.SetHandle(display);
                return opened;
            }

            device.Dispose();
            failure = new($"The driver for {path} did not start", status);
        }

        return null;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _device?.Dispose();
        }
    }

    /// <inheritdoc/>
    protected override bool ReleaseHandle() => NativeMethods.Terminate(handle) == 0;
}
