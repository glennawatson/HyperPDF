// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdf.Media.Native.VaApi;

/// <summary>A <c>VAConfigAttrib</c>: one setting of a decoder configuration.</summary>
/// <param name="Type">Which setting.</param>
/// <param name="Value">The value.</param>
[DebuggerDisplay("ConfigAttribute: {Type} = {Value}")]
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct ConfigAttribute(int Type, uint Value);
