// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>The object ids of one window's protocol objects. An id of zero means the object does not exist yet.</summary>
internal sealed class WaylandObjects
{
    /// <summary>Gets or sets the wl_registry id.</summary>
    internal uint Registry { get; set; }

    /// <summary>Gets or sets the sync wl_callback id.</summary>
    internal uint Callback { get; set; }

    /// <summary>Gets or sets the wl_compositor id.</summary>
    internal uint Compositor { get; set; }

    /// <summary>Gets or sets the wl_shm id.</summary>
    internal uint Shm { get; set; }

    /// <summary>Gets or sets the xdg_wm_base id.</summary>
    internal uint WmBase { get; set; }

    /// <summary>Gets or sets the wl_seat id.</summary>
    internal uint Seat { get; set; }

    /// <summary>Gets or sets the wl_surface id.</summary>
    internal uint Surface { get; set; }

    /// <summary>Gets or sets the xdg_surface id.</summary>
    internal uint XdgSurface { get; set; }

    /// <summary>Gets or sets the zxdg_toplevel_decoration_v1 id, 0 when the compositor was not asked to draw the frame.</summary>
    internal uint Decoration { get; set; }

    /// <summary>Gets or sets the xdg_toplevel id.</summary>
    internal uint Toplevel { get; set; }

    /// <summary>Gets or sets the wl_pointer id.</summary>
    internal uint Pointer { get; set; }

    /// <summary>Gets or sets the wl_keyboard id.</summary>
    internal uint Keyboard { get; set; }

    /// <summary>Gets or sets the wl_output id, 0 when the compositor offers no display that reports a scale.</summary>
    internal uint Output { get; set; }

    /// <summary>Gets or sets the wp_fractional_scale_v1 id of the surface, 0 when the compositor only reports whole-number scales.</summary>
    internal uint FractionalScale { get; set; }

    /// <summary>Gets or sets the wp_viewport id of the surface, 0 when the surface is shown at its buffer's size.</summary>
    internal uint Viewport { get; set; }
}
