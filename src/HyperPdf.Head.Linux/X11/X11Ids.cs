// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Head.Linux.X11;

/// <summary>
/// The ids of an X11 window. They are numbers the X server owns and are released through the display connection, so they are held as
/// 64-bit numbers (the head targets 64-bit Linux) and converted at each call.
/// </summary>
/// <param name="Window">The window id.</param>
/// <param name="Gc">The graphics context id.</param>
/// <param name="Visual">The visual of the screen.</param>
/// <param name="CloseAtom">The close request protocol atom.</param>
[DebuggerDisplay("X11Ids window {Window}")]
internal readonly record struct X11Ids(long Window, long Gc, long Visual, long CloseAtom);
