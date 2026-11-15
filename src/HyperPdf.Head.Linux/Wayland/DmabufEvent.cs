// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Head.Linux.Wayland;

/// <summary>What the GPU buffer pool did with a compositor event.</summary>
internal enum DmabufEvent
{
    /// <summary>The event was not for the pool.</summary>
    None = 0,

    /// <summary>The event was for the pool and needs nothing more.</summary>
    Handled = 1,

    /// <summary>The compositor made the last buffer, so the window can draw a frame.</summary>
    BuffersReady = 2,
}
