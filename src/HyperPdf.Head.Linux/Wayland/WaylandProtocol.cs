// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>The interface names, versions and opcodes of the Wayland protocols the head speaks (core, wl_shm and xdg-shell).</summary>
internal static class WaylandProtocol
{
    /// <summary>The wl_compositor interface name.</summary>
    internal const string CompositorInterface = "wl_compositor";

    /// <summary>The wl_shm interface name.</summary>
    internal const string ShmInterface = "wl_shm";

    /// <summary>The xdg_wm_base interface name.</summary>
    internal const string WmBaseInterface = "xdg_wm_base";

    /// <summary>The wl_seat interface name.</summary>
    internal const string SeatInterface = "wl_seat";

    /// <summary>The wl_compositor version the head binds, which has damage_buffer on surfaces.</summary>
    internal const uint CompositorVersion = 4;

    /// <summary>The wl_shm version the head binds.</summary>
    internal const uint ShmVersion = 1;

    /// <summary>The xdg_wm_base version the head binds.</summary>
    internal const uint WmBaseVersion = 1;

    /// <summary>The wl_seat version the head binds.</summary>
    internal const uint SeatVersion = 5;

    /// <summary>The wl_display.sync request.</summary>
    internal const int DisplaySync = 0;

    /// <summary>The wl_display.get_registry request.</summary>
    internal const int DisplayGetRegistry = 1;

    /// <summary>The wl_registry.bind request.</summary>
    internal const int RegistryBind = 0;

    /// <summary>The wl_compositor.create_surface request.</summary>
    internal const int CompositorCreateSurface = 0;

    /// <summary>The wl_surface.attach request.</summary>
    internal const int SurfaceAttach = 1;

    /// <summary>The wl_surface.commit request.</summary>
    internal const int SurfaceCommit = 6;

    /// <summary>The wl_surface.damage_buffer request.</summary>
    internal const int SurfaceDamageBuffer = 9;

    /// <summary>The wl_surface.set_buffer_scale request.</summary>
    internal const int SurfaceSetBufferScale = 8;

    /// <summary>The interface name of a display global.</summary>
    internal const string OutputInterface = "wl_output";

    /// <summary>The wl_output version the head binds, the first that reports a scale.</summary>
    internal const uint OutputVersion = 2;

    /// <summary>The wl_output.scale event.</summary>
    internal const int OutputScale = 3;

    /// <summary>The xdg_wm_base.get_xdg_surface request.</summary>
    internal const int WmBaseGetXdgSurface = 2;

    /// <summary>The xdg_wm_base.pong request.</summary>
    internal const int WmBasePong = 3;

    /// <summary>The xdg_wm_base.ping event.</summary>
    internal const int WmBasePing = 0;

    /// <summary>The xdg_surface.get_toplevel request.</summary>
    internal const int XdgSurfaceGetToplevel = 1;

    /// <summary>The xdg_surface.ack_configure request.</summary>
    internal const int XdgSurfaceAckConfigure = 4;

    /// <summary>The xdg_toplevel.set_title request.</summary>
    internal const int ToplevelSetTitle = 2;

    /// <summary>The xdg_toplevel.set_app_id request.</summary>
    internal const int ToplevelSetAppId = 3;

    /// <summary>The wl_seat.get_pointer request.</summary>
    internal const int SeatGetPointer = 0;

    /// <summary>The wl_seat.get_keyboard request.</summary>
    internal const int SeatGetKeyboard = 1;

    /// <summary>The wl_pointer.enter event.</summary>
    internal const int PointerEnter = 0;

    /// <summary>The wl_pointer.motion event.</summary>
    internal const int PointerMotion = 2;

    /// <summary>The wl_pointer.button event.</summary>
    internal const int PointerButton = 3;

    /// <summary>The wl_pointer.axis event.</summary>
    internal const int PointerAxis = 4;

    /// <summary>The wl_keyboard.key event.</summary>
    internal const int KeyboardKey = 3;

    /// <summary>The wl_keyboard.modifiers event.</summary>
    internal const int KeyboardModifiers = 4;

    /// <summary>The wl_keyboard.keymap event, which carries a file descriptor.</summary>
    internal const int KeyboardKeymap = 0;

    /// <summary>The interface name of the clipboard and drag manager global.</summary>
    internal const string DataDeviceManagerInterface = "wl_data_device_manager";

    /// <summary>The newest wl_data_device_manager version the head implements.</summary>
    internal const uint DataDeviceManagerVersion = 3;

    /// <summary>The wl_data_device_manager.create_data_source request.</summary>
    internal const int ManagerCreateDataSource = 0;

    /// <summary>The wl_data_device_manager.get_data_device request.</summary>
    internal const int ManagerGetDataDevice = 1;

    /// <summary>The wl_data_source.offer request.</summary>
    internal const int DataSourceOffer = 0;

    /// <summary>The wl_data_source.destroy request.</summary>
    internal const int DataSourceDestroy = 1;

    /// <summary>The wl_data_source.send event, which carries a file descriptor.</summary>
    internal const int DataSourceSend = 1;

    /// <summary>The wl_data_source.cancelled event.</summary>
    internal const int DataSourceCancelled = 2;

    /// <summary>The wl_data_device.set_selection request.</summary>
    internal const int DataDeviceSetSelection = 1;

    /// <summary>The wl_data_device.data_offer event, which names a new object.</summary>
    internal const int DataDeviceDataOffer = 0;

    /// <summary>The wl_data_offer.destroy request.</summary>
    internal const int DataOfferDestroy = 2;

    /// <summary>The wl_data_offer.receive request, which carries a file descriptor.</summary>
    internal const int DataOfferReceive = 1;

    /// <summary>The wl_data_offer.offer event, which names one kind of data the offer has.</summary>
    internal const int DataOfferOffer = 0;

    /// <summary>The wl_data_device.selection event, which names the offer that is the clipboard.</summary>
    internal const int DataDeviceSelection = 5;
}
