// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdf.Media.Native.VideoToolbox;

/// <summary>A <c>CMSampleTimingInfo</c>: when a coded picture is decoded, shown, and for how long.</summary>
/// <param name="Duration">How long the picture is shown.</param>
/// <param name="PresentationTime">When the picture is shown.</param>
/// <param name="DecodeTime">When the picture is decoded.</param>
[DebuggerDisplay("SampleTiming: shown at {PresentationTime}")]
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct SampleTiming(CoreMediaTime Duration, CoreMediaTime PresentationTime, CoreMediaTime DecodeTime);
