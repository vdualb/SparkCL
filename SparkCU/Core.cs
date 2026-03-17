using ManagedCuda;
using OCLHelper;
using Silk.NET.OpenCL;
using System.Diagnostics;

namespace SparkCU;

static public class StarterKit
{
    // создать объекты на первом попавшемся GPU
    static public void GetStarterKit(
        out PrimaryContext context
    )
    {
        Trace.Indent();

        var sw = Stopwatch.StartNew();
        int numDevices = CudaContext.GetDeviceCount();
        if (numDevices == 0)
        {
            Console.WriteLine("Error: Couldn't find CUDA capable device");
        }
        Console.WriteLine($"Found CUDA capable devices. Count: {numDevices}");
        int deviceNum = 0;
        context = new PrimaryContext(deviceNum);
        Trace.WriteLine($"Init: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        CudaDeviceProperties deviceProp = CudaContext.GetDeviceInfo(deviceNum);
        Trace.WriteLine($"Query info: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Driver version: {deviceProp.DriverVersion}");
        Console.WriteLine($"Device: {deviceProp.DeviceName}");
        Console.WriteLine($"Compute capability: {deviceProp.ComputeCapability}");

        Trace.Unindent();
    }
}

static public class Core
{
    public const nuint Prefered1D = 32;

    // Every object stored in static memory must be disposed
    // together with Core. Such objects can't be reused across
    // OpenCL contexts, otherwise CL_INVALID_CONTEXT occurs.
    // This event is introduced to connect handlers which dispose
    // mentioned objects.
    public delegate void DeinitHandler();
    static public event DeinitHandler? OnDeinit;
    
    static public PrimaryContext? context;
    static public OCLHelper.Context clContext;
    static public OCLHelper.Device clDevice;
#if CU_COLLECT_TIME
    static public List<Event> IOEvents { get; private set; } = new(32);
    static public List<Event> KernEvents { get; private set; } = new(32);
#endif
    static public void Init()
    {
        Trace.Indent();

        // OpenCL init
        Trace.Indent();
        var sw = Stopwatch.StartNew();
        var platforms = Platform.GetDiscovered();
        Trace.WriteLine($"Discover platforms: {sw.ElapsedMilliseconds}ms");
        Platform platform;

        if (clDevice is not null)
        {
            Console.WriteLine("Warning: SparkCL was already initialized");
        }

        // Avoid Clover if possible
        if (platforms[0].GetName() == "Clover" && platforms.Length > 1)
        {
            platform = platforms[1];
        }
        else
        {
            platform = platforms[0];
        }

        Console.WriteLine($"Platform: {platform.GetName()}");
        Console.WriteLine($"Version: {platform.GetVersion()}");

        sw.Restart();
        clDevice = platform.GetDevicesOfType(DeviceType.Gpu).First();
        Trace.WriteLine($"List devices: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Device: {clDevice.GetName()}");

        sw.Restart();
        clContext = Context.ForDevices([clDevice]);
        Trace.WriteLine($"Create context: {sw.ElapsedMilliseconds}ms");
        //

        if (context is not null)
        {
            Console.WriteLine("Warning: SparkCU was already initialized");
        }

        sw.Restart();
        int numDevices = CudaContext.GetDeviceCount();
        if (numDevices == 0)
        {
            Console.WriteLine("Error: Couldn't find a CUDA capable device");
        }
        Console.WriteLine($"Found a CUDA capable devices. Count: {numDevices}");
        int deviceNum = 0;
        context = new PrimaryContext(deviceNum);
        context.SetCurrent();
        Trace.WriteLine($"Init: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        var drv = CudaContext.GetDriverVersion();
        var device_name = CudaContext.GetDeviceName(deviceNum);
        var compute = CudaContext.GetDeviceComputeCapability(deviceNum);

        Trace.WriteLine($"Query info: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Driver version: {drv}");
        Console.WriteLine($"Device: {device_name}");
        Console.WriteLine($"Compute capability: {compute}");
        
        Trace.Unindent();
    }

    static public void Deinit()
    {
        OnDeinit?.Invoke();
        OnDeinit = null;

        context?.Dispose();
        context = null;
    }

#if CU_COLLECT_TIME
    // Должна быть вызвана после завершения всех операций на устройстве
    static public (ulong IOTime, ulong KernTime) MeasureTime()
    {
        ulong IO = 0;
        ulong Kern = 0;

        foreach (var ev in IOEvents)
        {
            IO += ev.GetElapsed();
        }
        foreach (var ev in KernEvents)
        {
            Kern += ev.GetElapsed();
        }
        KernEvents.Clear();
        IOEvents.Clear();

        return (IO, Kern);
    }
    static public void ResetTime()
    {
        KernEvents.Clear();
        IOEvents.Clear();
    }
#endif


    /* 
    static public void WaitQueue()
    {
        queue!.Finish();
    }
    */
}
