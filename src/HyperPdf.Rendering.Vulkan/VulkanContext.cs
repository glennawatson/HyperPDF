// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
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

    /// <summary>The number of instance extensions needed to present to an X11 window.</summary>
    private const uint SurfaceExtensionCount = 2;

    /// <summary>The most device extensions the context enables: two for dmabufs and one for the swapchain.</summary>
    private const int MaxDeviceExtensions = 3;

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

    /// <summary>The device extension that presents images to a window.</summary>
    private static readonly byte[] SwapchainExtension = "VK_KHR_swapchain\0"u8.ToArray();

    /// <summary>The instance extension that every window surface builds on.</summary>
    private static readonly byte[] SurfaceExtension = "VK_KHR_surface\0"u8.ToArray();

    /// <summary>The instance extension that makes a surface from an X11 window.</summary>
    private static readonly byte[] XlibSurfaceExtension = "VK_KHR_xlib_surface\0"u8.ToArray();

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
        PhysicalDevice = selection.PhysicalDevice;
        ulong queue;
        NativeMethods.GetDeviceQueue(device, selection.QueueFamily, 0, &queue);
        Queue = queue;
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

    /// <summary>Gets a value indicating whether the context can present frames to a window through a swapchain.</summary>
    public bool CanPresent { get; private init; }

    /// <summary>Gets the instance handle.</summary>
    internal ulong Instance => _instance;

    /// <summary>Gets the device handle.</summary>
    internal ulong Device => _device;

    /// <summary>Gets the physical device handle.</summary>
    internal ulong PhysicalDevice { get; }

    /// <summary>Gets the graphics queue handle.</summary>
    internal ulong Queue { get; }

    /// <summary>Creates a context on the best GPU.</summary>
    /// <returns>The context, or <see langword="null"/> when the machine has no Vulkan loader, driver or graphics queue.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static VulkanContext? TryCreate() => TryCreate(forWindow: false);

    /// <summary>Creates a context on the best GPU.</summary>
    /// <param name="forWindow">Whether to ask for X11 window presentation. When the driver has none, the context is still made and <see cref="CanPresent"/> is <see langword="false"/>.</param>
    /// <returns>The context, or <see langword="null"/> when the machine has no Vulkan loader, driver or graphics queue.</returns>
    public static VulkanContext? TryCreate(bool forWindow)
    {
        NativeLibraries.Register(typeof(NativeMethods).Assembly, NativeMethods.Library, LoaderNames);
        try
        {
            return Create(forWindow);
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
    /// <param name="forWindow">Whether to ask for X11 window presentation.</param>
    /// <returns>The context.</returns>
    /// <exception cref="PlatformNotSupportedException">The machine has no usable GPU.</exception>
    private static VulkanContext Create(bool forWindow)
    {
        var windowInstance = forWindow ? CreateWindowInstance() : 0;
        var instance = windowInstance != 0 ? windowInstance : CreateInstance();
        try
        {
            var selection = SelectGpu(instance);
            return new(instance, selection, CreateDevice(selection)) { CanPresent = windowInstance != 0 && selection.HasSwapchain };
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
            var instance = CreateInstanceWith(version, 0, 0);
            if (instance != 0)
            {
                return instance;
            }
        }

        throw new PlatformNotSupportedException("No Vulkan driver accepted an instance.");
    }

    /// <summary>Creates an instance that can present to X11 windows, or reports that the driver cannot.</summary>
    /// <returns>The instance handle, or 0 when no driver has the X11 surface extensions.</returns>
    private static ulong CreateWindowInstance()
    {
        fixed (byte* surface = SurfaceExtension)
        {
            fixed (byte* xlib = XlibSurfaceExtension)
            {
                ulong* names = stackalloc ulong[] { (ulong)surface, (ulong)xlib };
                foreach (var version in (ReadOnlySpan<uint>)[VkConstants.ApiVersion11, VkConstants.ApiVersion10])
                {
                    var instance = CreateInstanceWith(version, SurfaceExtensionCount, (ulong)names);
                    if (instance != 0)
                    {
                        return instance;
                    }
                }
            }
        }

        return 0;
    }

    /// <summary>Creates an instance for one API version and set of extensions.</summary>
    /// <param name="version">The API version asked for.</param>
    /// <param name="extensionCount">The number of instance extensions.</param>
    /// <param name="extensionNames">A pointer to the extension name pointers, or 0.</param>
    /// <returns>The instance handle, or 0 when the driver refused.</returns>
    private static ulong CreateInstanceWith(uint version, uint extensionCount, ulong extensionNames)
    {
        var application = new VkApplicationInfo { SType = VkConstants.ApplicationInfoType, ApiVersion = version };
        var info = new VkInstanceCreateInfo
        {
            SType = VkConstants.InstanceCreateInfoType,
            ApplicationInfo = (ulong)&application,
            EnabledExtensionCount = extensionCount,
            EnabledExtensionNames = extensionNames,
        };
        ulong instance;
        return NativeMethods.CreateInstance(&info, null, &instance) == VkConstants.Success ? instance : 0;
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
        var extensions = ReadExtensions(physicalDevice);
        return new(physicalDevice, ReadName(properties + VkConstants.DeviceNameOffset), (uint)family, score, extensions.Dmabuf, extensions.Swapchain);
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

    /// <summary>Finds the optional extensions a GPU has: the two that export memory as a dmabuf and the swapchain.</summary>
    /// <param name="physicalDevice">The GPU.</param>
    /// <returns>What the GPU has.</returns>
    private static GpuExtensions ReadExtensions(ulong physicalDevice)
    {
        uint count = MaxExtensions;
        byte* extensions = stackalloc byte[MaxExtensions * VkConstants.ExtensionPropertiesSize];
        _ = NativeMethods.EnumerateDeviceExtensionProperties(physicalDevice, null, &count, extensions);
        var fd = false;
        var dmabuf = false;
        var swapchain = false;
        for (var i = 0; i < count; i++)
        {
            var name = new ReadOnlySpan<byte>(extensions + (i * VkConstants.ExtensionPropertiesSize), VkConstants.ExtensionNameLength);
            var length = name.IndexOf((byte)0);
            var trimmed = length < 0 ? name : name[..length];
            fd |= trimmed.SequenceEqual(ExternalMemoryFd.AsSpan(0, ExternalMemoryFd.Length - 1));
            dmabuf |= trimmed.SequenceEqual(ExternalMemoryDmaBuf.AsSpan(0, ExternalMemoryDmaBuf.Length - 1));
            swapchain |= trimmed.SequenceEqual(SwapchainExtension.AsSpan(0, SwapchainExtension.Length - 1));
        }

        return new(fd && dmabuf, swapchain);
    }

    /// <summary>Creates the logical device with one graphics queue, and the dmabuf export and swapchain extensions when the GPU has them.</summary>
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
                fixed (byte* swapchain = SwapchainExtension)
                {
                    ulong* names = stackalloc ulong[MaxDeviceExtensions];
                    var count = 0U;
                    if (selection.CanExportDmabuf)
                    {
                        names[0] = (ulong)fd;
                        names[1] = (ulong)dmabuf;
                        count = DmabufExtensionCount;
                    }

                    if (selection.HasSwapchain)
                    {
                        names[count] = (ulong)swapchain;
                        count++;
                    }

                    var info = new VkDeviceCreateInfo
                    {
                        SType = VkConstants.DeviceCreateInfoType,
                        QueueCreateInfoCount = 1,
                        QueueCreateInfos = (ulong)&queue,
                        EnabledExtensionCount = count,
                        EnabledExtensionNames = count == 0 ? 0 : (ulong)names,
                    };
                    ulong device;
                    return NativeMethods.CreateDevice(selection.PhysicalDevice, &info, null, &device) == VkConstants.Success
                        ? device
                        : throw new PlatformNotSupportedException("The GPU refused to create a Vulkan device.");
                }
            }
        }
    }
}
