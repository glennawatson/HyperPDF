// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using HyperPdf.Core.Platform;
using HyperPdf.Rendering.Vulkan.Interop;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>A Vulkan instance, the best GPU on the machine, a logical device and its graphics queue.</summary>
[DebuggerDisplay("VulkanContext: {DeviceName}")]
public sealed unsafe class VulkanContext : IDisposable
{
    /// <summary>The most GPUs the context looks at.</summary>
    private const int MaxDevices = 16;

    /// <summary>The most queue families the context looks at on one GPU.</summary>
    private const int MaxQueueFamilies = 32;

    /// <summary>The most extensions the context reads from one GPU.</summary>
    private const int MaxExtensions = 512;

    /// <summary>The queue priority of the one graphics queue.</summary>
    private const float QueuePriority = 1F;

    /// <summary>The number of device extensions needed to export a dmabuf.</summary>
    private const uint DmabufExtensionCount = 2;

    /// <summary>The score of a discrete GPU.</summary>
    private const int DiscreteScore = 3;

    /// <summary>The score of an integrated GPU.</summary>
    private const int IntegratedScore = 2;

    /// <summary>The score of any other GPU, such as a software renderer.</summary>
    private const int OtherScore = 1;

    /// <summary>The device extension that shares memory as file descriptors.</summary>
    private static readonly byte[] ExternalMemoryFd = "VK_KHR_external_memory_fd\0"u8.ToArray();

    /// <summary>The device extension that lets those descriptors be dmabufs.</summary>
    private static readonly byte[] ExternalMemoryDmaBuf = "VK_EXT_external_memory_dma_buf\0"u8.ToArray();

    /// <summary>The names the Vulkan loader goes by on Windows, Linux and macOS.</summary>
    private static readonly string[] LoaderNames = ["vulkan-1.dll", "libvulkan.so.1", "libvulkan.so", "libvulkan.1.dylib", "libMoltenVK.dylib"];

    /// <summary>The instance handle.</summary>
    private readonly ulong _instance;

    /// <summary>The device handle.</summary>
    private readonly ulong _device;

    /// <summary>Initializes a new instance of the <see cref="VulkanContext"/> class.</summary>
    /// <param name="instance">The instance handle.</param>
    /// <param name="selection">The chosen GPU.</param>
    /// <param name="device">The device handle.</param>
    private VulkanContext(ulong instance, in GpuSelection selection, ulong device)
    {
        _instance = instance;
        _device = device;
        DeviceName = selection.Name;
        QueueFamily = selection.QueueFamily;
        CanExportDmabuf = selection.CanExportDmabuf;
    }

    /// <summary>Gets the name of the GPU.</summary>
    public string DeviceName { get; }

    /// <summary>Gets the graphics queue family index.</summary>
    public uint QueueFamily { get; }

    /// <summary>Gets a value indicating whether the GPU can export an image as a dmabuf, which is how frames reach a Wayland compositor.</summary>
    public bool CanExportDmabuf { get; }

    /// <summary>Creates a context on the best GPU.</summary>
    /// <returns>The context, or <see langword="null"/> when the machine has no Vulkan loader, driver or graphics queue.</returns>
    public static VulkanContext? TryCreate()
    {
        NativeLibraries.Register(typeof(NativeMethods).Assembly, NativeMethods.Library, LoaderNames);
        try
        {
            return Create();
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException or PlatformNotSupportedException)
        {
            // No loader, driver or GPU is an expected reason for the head to use the CPU backend, not a fault.
            return null;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _ = NativeMethods.DeviceWaitIdle(_device);
        NativeMethods.DestroyDevice(_device, null);
        NativeMethods.DestroyInstance(_instance, null);
    }

    /// <summary>Creates the instance and device.</summary>
    /// <returns>The context.</returns>
    /// <exception cref="PlatformNotSupportedException">The machine has no usable GPU.</exception>
    private static VulkanContext Create()
    {
        var instance = CreateInstance();
        try
        {
            var selection = SelectGpu(instance);
            return new(instance, selection, CreateDevice(selection));
        }
        catch (PlatformNotSupportedException)
        {
            NativeMethods.DestroyInstance(instance, null);
            throw;
        }
    }

    /// <summary>Creates an instance, asking for Vulkan 1.1 and settling for 1.0.</summary>
    /// <returns>The instance handle.</returns>
    /// <exception cref="PlatformNotSupportedException">No Vulkan driver accepted an instance.</exception>
    private static ulong CreateInstance()
    {
        foreach (var version in (ReadOnlySpan<uint>)[VkConstants.ApiVersion11, VkConstants.ApiVersion10])
        {
            var application = new VkApplicationInfo { SType = VkConstants.ApplicationInfoType, ApiVersion = version };
            var info = new VkInstanceCreateInfo { SType = VkConstants.InstanceCreateInfoType, ApplicationInfo = (ulong)&application };
            ulong instance;
            if (NativeMethods.CreateInstance(&info, null, &instance) == VkConstants.Success)
            {
                return instance;
            }
        }

        throw new PlatformNotSupportedException("No Vulkan driver accepted an instance.");
    }

    /// <summary>Picks the GPU: discrete first, then integrated, then anything that has a graphics queue.</summary>
    /// <param name="instance">The instance.</param>
    /// <returns>The chosen GPU.</returns>
    /// <exception cref="PlatformNotSupportedException">No GPU has a graphics queue.</exception>
    private static GpuSelection SelectGpu(ulong instance)
    {
        uint count = MaxDevices;
        ulong* devices = stackalloc ulong[MaxDevices];
        _ = NativeMethods.EnumeratePhysicalDevices(instance, &count, devices);
        GpuSelection? best = null;
        for (var i = 0; i < count; i++)
        {
            if (Inspect(devices[i]) is { } candidate && candidate.Score > (best?.Score ?? 0))
            {
                best = candidate;
            }
        }

        return best ?? throw new PlatformNotSupportedException("No GPU has a graphics queue.");
    }

    /// <summary>Reads what a GPU offers.</summary>
    /// <param name="physicalDevice">The GPU.</param>
    /// <returns>The description, or <see langword="null"/> when the GPU has no graphics queue.</returns>
    private static GpuSelection? Inspect(ulong physicalDevice)
    {
        var family = FindGraphicsFamily(physicalDevice);
        if (family < 0)
        {
            return null;
        }

        byte* properties = stackalloc byte[VkConstants.PhysicalDevicePropertiesSize];
        NativeMethods.GetPhysicalDeviceProperties(physicalDevice, properties);
        var score = *(uint*)(properties + VkConstants.DeviceTypeOffset) switch
        {
            VkConstants.DiscreteGpu => DiscreteScore,
            VkConstants.IntegratedGpu => IntegratedScore,
            _ => OtherScore,
        };
        return new(physicalDevice, ReadName(properties + VkConstants.DeviceNameOffset), (uint)family, score, HasDmabufExport(physicalDevice));
    }

    /// <summary>Finds the first queue family that can draw.</summary>
    /// <param name="physicalDevice">The GPU.</param>
    /// <returns>The family index, or -1 when there is none.</returns>
    private static int FindGraphicsFamily(ulong physicalDevice)
    {
        uint count = MaxQueueFamilies;
        byte* families = stackalloc byte[MaxQueueFamilies * VkConstants.QueueFamilyPropertiesSize];
        NativeMethods.GetPhysicalDeviceQueueFamilyProperties(physicalDevice, &count, families);
        for (var i = 0; i < count; i++)
        {
            if ((*(uint*)(families + (i * VkConstants.QueueFamilyPropertiesSize)) & VkConstants.QueueGraphicsBit) != 0)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Reads a null terminated UTF-8 name from a fixed size buffer.</summary>
    /// <param name="name">The buffer start.</param>
    /// <returns>The name.</returns>
    private static string ReadName(byte* name)
    {
        var length = 0;
        while (length < VkConstants.DeviceNameLength && name[length] != 0)
        {
            length++;
        }

        return Encoding.UTF8.GetString(name, length);
    }

    /// <summary>Tells whether a GPU has the two extensions that export memory as a dmabuf.</summary>
    /// <param name="physicalDevice">The GPU.</param>
    /// <returns><see langword="true"/> when both are there.</returns>
    private static bool HasDmabufExport(ulong physicalDevice)
    {
        uint count = MaxExtensions;
        byte* extensions = stackalloc byte[MaxExtensions * VkConstants.ExtensionPropertiesSize];
        _ = NativeMethods.EnumerateDeviceExtensionProperties(physicalDevice, null, &count, extensions);
        var fd = false;
        var dmabuf = false;
        for (var i = 0; i < count; i++)
        {
            var name = new ReadOnlySpan<byte>(extensions + (i * VkConstants.ExtensionPropertiesSize), VkConstants.ExtensionNameLength);
            var length = name.IndexOf((byte)0);
            var trimmed = length < 0 ? name : name[..length];
            fd |= trimmed.SequenceEqual(ExternalMemoryFd.AsSpan(0, ExternalMemoryFd.Length - 1));
            dmabuf |= trimmed.SequenceEqual(ExternalMemoryDmaBuf.AsSpan(0, ExternalMemoryDmaBuf.Length - 1));
        }

        return fd && dmabuf;
    }

    /// <summary>Creates the logical device with one graphics queue, and the dmabuf export extensions when the GPU has them.</summary>
    /// <param name="selection">The chosen GPU.</param>
    /// <returns>The device handle.</returns>
    /// <exception cref="PlatformNotSupportedException">The GPU refused to create a device.</exception>
    private static ulong CreateDevice(in GpuSelection selection)
    {
        var priority = QueuePriority;
        var queue = new VkDeviceQueueCreateInfo { SType = VkConstants.DeviceQueueCreateInfoType, QueueFamilyIndex = selection.QueueFamily, QueueCount = 1, QueuePriorities = (ulong)&priority };
        fixed (byte* fd = ExternalMemoryFd)
        {
            fixed (byte* dmabuf = ExternalMemoryDmaBuf)
            {
                ulong* names = stackalloc ulong[] { (ulong)fd, (ulong)dmabuf };
                var info = new VkDeviceCreateInfo
                {
                    SType = VkConstants.DeviceCreateInfoType,
                    QueueCreateInfoCount = 1,
                    QueueCreateInfos = (ulong)&queue,
                    EnabledExtensionCount = selection.CanExportDmabuf ? DmabufExtensionCount : 0,
                    EnabledExtensionNames = selection.CanExportDmabuf ? (ulong)names : 0,
                };
                ulong device;
                return NativeMethods.CreateDevice(selection.PhysicalDevice, &info, null, &device) == VkConstants.Success
                    ? device
                    : throw new PlatformNotSupportedException("The GPU refused to create a Vulkan device.");
            }
        }
    }
}
