// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>The globals the head needs, as the compositor advertised them.</summary>
internal sealed class WaylandGlobals
{
    /// <summary>Gets the compositor global.</summary>
    internal WaylandGlobal Compositor { get; private set; }

    /// <summary>Gets the shared memory global.</summary>
    internal WaylandGlobal Shm { get; private set; }

    /// <summary>Gets the xdg shell global.</summary>
    internal WaylandGlobal WmBase { get; private set; }

    /// <summary>Gets the seat global, which stays empty on a machine with no input device.</summary>
    internal WaylandGlobal Seat { get; private set; }

    /// <summary>Gets the clipboard manager global, which stays empty on a compositor that has none.</summary>
    internal WaylandGlobal DataDeviceManager { get; private set; }

    /// <summary>Gets the first display global that reports a scale, which stays empty on a compositor too old to report one.</summary>
    internal WaylandGlobal Output { get; private set; }

    /// <summary>Keeps a global when it is one the head uses.</summary>
    /// <param name="interfaceName">The interface name.</param>
    /// <param name="global">The advertised global.</param>
    internal void Record(string? interfaceName, in WaylandGlobal global)
    {
        switch (interfaceName)
        {
            case WaylandProtocol.CompositorInterface:
                {
                    Compositor = global;
                    break;
                }

            case WaylandProtocol.ShmInterface:
                {
                    Shm = global;
                    break;
                }

            case WaylandProtocol.WmBaseInterface:
                {
                    WmBase = global;
                    break;
                }

            case WaylandProtocol.SeatInterface when Seat.Name == 0:
                {
                    Seat = global;
                    break;
                }

            case WaylandProtocol.DataDeviceManagerInterface:
                {
                    DataDeviceManager = global;
                    break;
                }

            case WaylandProtocol.OutputInterface when Output.Name == 0 && global.Version >= WaylandProtocol.OutputVersion:
                {
                    Output = global;
                    break;
                }
        }
    }
}
