// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>An image layout change and the work it waits for and holds back.</summary>
/// <param name="OldLayout">The layout the image is in.</param>
/// <param name="NewLayout">The layout the image moves to.</param>
/// <param name="SourceAccess">The access the earlier work made.</param>
/// <param name="DestinationAccess">The access the later work needs.</param>
/// <param name="SourceStage">The pipeline stage of the earlier work.</param>
/// <param name="DestinationStage">The pipeline stage of the later work.</param>
[DebuggerDisplay("ImageTransition {OldLayout} to {NewLayout}")]
internal readonly record struct ImageTransition(uint OldLayout, uint NewLayout, uint SourceAccess, uint DestinationAccess, uint SourceStage, uint DestinationStage);
