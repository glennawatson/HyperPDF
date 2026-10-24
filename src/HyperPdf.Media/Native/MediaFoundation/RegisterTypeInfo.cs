// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdf.Media.Native.MediaFoundation;

/// <summary>A major type and subtype, as Media Foundation lists what a transform takes or gives (<c>MFT_REGISTER_TYPE_INFO</c>).</summary>
/// <param name="MajorType">The major type, such as video.</param>
/// <param name="Subtype">The subtype, such as H.264.</param>
[StructLayout(LayoutKind.Sequential)]
[DebuggerDisplay("RegisterTypeInfo: {MajorType} {Subtype}")]
internal readonly record struct RegisterTypeInfo(Guid MajorType, Guid Subtype);
