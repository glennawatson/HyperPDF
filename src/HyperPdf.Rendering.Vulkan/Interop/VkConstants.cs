// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Vulkan.Interop;

/// <summary>The Vulkan enumerants, structure types and layout offsets the backend uses.</summary>
internal static class VkConstants
{
    /// <summary>The VkResult that means the call worked.</summary>
    internal const int Success = 0;

    /// <summary>The structure type of VkApplicationInfo.</summary>
    internal const uint ApplicationInfoType = 0;

    /// <summary>The structure type of VkInstanceCreateInfo.</summary>
    internal const uint InstanceCreateInfoType = 1;

    /// <summary>The structure type of VkDeviceQueueCreateInfo.</summary>
    internal const uint DeviceQueueCreateInfoType = 2;

    /// <summary>The structure type of VkDeviceCreateInfo.</summary>
    internal const uint DeviceCreateInfoType = 3;

    /// <summary>The Vulkan 1.0 version number, which every Vulkan driver supports.</summary>
    internal const uint ApiVersion10 = 1U << 22;

    /// <summary>The Vulkan 1.1 version number, which has the external memory functions in core.</summary>
    internal const uint ApiVersion11 = (1U << 22) | (1U << 12);

    /// <summary>The queue flag bit for graphics work.</summary>
    internal const uint QueueGraphicsBit = 1;

    /// <summary>The physical device type of an integrated GPU.</summary>
    internal const uint IntegratedGpu = 1;

    /// <summary>The physical device type of a discrete GPU.</summary>
    internal const uint DiscreteGpu = 2;

    /// <summary>The size of VkPhysicalDeviceProperties in bytes.</summary>
    internal const int PhysicalDevicePropertiesSize = 824;

    /// <summary>The offset of deviceType in VkPhysicalDeviceProperties.</summary>
    internal const int DeviceTypeOffset = 16;

    /// <summary>The offset of deviceName in VkPhysicalDeviceProperties.</summary>
    internal const int DeviceNameOffset = 20;

    /// <summary>The length of deviceName in bytes.</summary>
    internal const int DeviceNameLength = 256;

    /// <summary>The size of VkQueueFamilyProperties in bytes.</summary>
    internal const int QueueFamilyPropertiesSize = 24;

    /// <summary>The size of VkExtensionProperties in bytes.</summary>
    internal const int ExtensionPropertiesSize = 260;

    /// <summary>The length of an extension name in VkExtensionProperties.</summary>
    internal const int ExtensionNameLength = 256;
}
