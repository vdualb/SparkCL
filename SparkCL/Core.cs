using System.Diagnostics;

using OCLHelper;
using Silk.NET.OpenCL;

// идея сократить область применения до вычисления на одном устройстве.
// это должно упростить использование OpenCL, абстрагируя понятия контекста,
// очереди команд и устройства.
namespace SparkCL;

static public class StarterKit
{
    // создать объекты на первом попавшемся GPU
    static public void GetStarterKit(
        out OCLHelper.Context context,
        out OCLHelper.Device device,
        out OCLHelper.CommandQueue commandQueue)
    {
        var platforms = Platform.GetDiscovered();

        Platform platform;
        // Avoid Clover if possible
        if (platforms[0].GetName() == "Clover" && platforms.Length > 1)
        {
            platform = platforms[1];
        } else {
            platform = platforms[0];
        }

        Console.WriteLine($"Platform: {platform.GetName()}");
        Console.WriteLine($"Version: {platform.GetVersion()}");

        device = platform.GetDevicesOfType(DeviceType.Gpu).First();

        Console.WriteLine($"Device: {device.GetName()}");

        context = Context.ForDevices([device]);

        CommandQueueProperties properties = CommandQueueProperties.None;
#if COLLECT_TIME
        properties |= CommandQueueProperties.ProfilingEnable;
#endif
        commandQueue = new CommandQueue(context, device, properties);
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
     
    static public Context? context;
    static public CommandQueue? queue;
    static public OCLHelper.Device? device;
#if COLLECT_TIME
    static public List<SparkCL.Event> IOEvents { get; private set; } = new(32);
    static public List<SparkCL.Event> KernEvents { get; private set; } = new(32);
#endif
    static public void Init()
    {
        Trace.Indent();
        var sw = Stopwatch.StartNew();
        var platforms = Platform.GetDiscovered();
        Trace.WriteLine($"Discover platforms: {sw.ElapsedMilliseconds}ms");
        Platform platform;
        
        if (device is not null) {
            Console.WriteLine("Warning: SparkCL was already initialized");
        }
        
        // Avoid Clover if possible
        if (platforms[0].GetName() == "Clover" && platforms.Length > 1)
        {
            platform = platforms[1];
        } else {
            platform = platforms[0];
        }

        Console.WriteLine($"Platform: {platform.GetName()}");
        Trace.WriteLine($"Platform: {platform.GetName()}");
        Console.WriteLine($"Version: {platform.GetVersion()}");
        Trace.WriteLine($"Version: {platform.GetVersion()}");

        sw.Restart();
        device = platform.GetDevicesOfType(DeviceType.Gpu).First();
        Trace.WriteLine($"List devices: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"Device: {device.GetName()}");
        Trace.WriteLine($"Device: {device.GetName()}");

        sw.Restart();
        context = Context.ForDevices([device]);
        Trace.WriteLine($"Create context: {sw.ElapsedMilliseconds}ms");

        // requires opencl 2.0
        // QueueProperties[] properties = [
#if COLLECT_TIME
        //     (QueueProperties)CommandQueueInfo.Properties, (QueueProperties) CommandQueueProperties.ProfilingEnable,
#endif
        //     0
        // ];
        CommandQueueProperties properties = CommandQueueProperties.None;
#if COLLECT_TIME
        properties |= CommandQueueProperties.ProfilingEnable;
#endif
        sw.Restart();
        queue = new CommandQueue(context, device, properties);
        Trace.WriteLine($"Create queue: {sw.ElapsedMilliseconds}ms");

        Console.WriteLine("OpenCL initialized");

        Trace.Unindent();
    }

    static public void Deinit()
    {
        OnDeinit?.Invoke();
        OnDeinit = null;
        context.Dispose();
        queue.Dispose();
        device.Dispose();
        
        context = null;
        queue = null;
        device = null;
    }

    #if COLLECT_TIME
    // Должна быть вызвана после завершения всех операций на устройстве
    static public (ulong IOTime, ulong KernTime) MeasureTime()
    {
        ulong IO = 0;
        ulong Kern = 0;

        foreach (var ev in IOEvents)
        {
            IO += ev.ElapsedMilliseconds;
        }
        foreach (var ev in KernEvents)
        {
            Kern += ev.ElapsedMilliseconds;
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

    static public void WaitQueue()
    {
        queue!.Finish();
    }
}
