// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Render.Managed;

/// <summary>Adds the managed drawing services to a registration.</summary>
public static class ManagedRenderRegistrationExtensions
{
    /// <summary>Extends a registration.</summary>
    /// <param name="registration">The registration.</param>
    extension(PdfRenderBackendRegistration registration)
    {
        /// <summary>Draws pages with the managed rasteriser. Fonts and image codecs already registered are kept.</summary>
        /// <returns>The registration with the managed backend.</returns>
        public PdfRenderBackendRegistration UseManaged()
        {
            ArgumentNullException.ThrowIfNull(registration);
            return registration with { Backend = new ManagedRenderBackend() };
        }
    }
}
