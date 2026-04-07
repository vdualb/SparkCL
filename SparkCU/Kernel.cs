using ManagedCuda;
using ManagedCuda.BasicTypes;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

using SparkCompute;

namespace SparkCU;

/*
public class ArgInfo
{
    public bool IsPointer { get; }
    public KernelArgAddressQualifier Qualifier { get; }
    public Type DataType { get; }
    public string TypeName { get; }

    public ArgInfo(string typeName, KernelArgAddressQualifier qualifier)
    {
        Qualifier = qualifier;
        TypeName = typeName;
        int base_end = typeName.LastIndexOf('*');
        if (base_end == typeName.Length - 1)
        {
            IsPointer = true;
            typeName = typeName[..base_end];
        } else {
            IsPointer = false;
        }

        DataType = typeName switch
        {
            "float" => typeof(float),
            "float4" => typeof(float),
            "double" => typeof(double),
            "int" => typeof(int),
            "uint" => typeof(uint),
            "uchar" => typeof(byte),
            "uchar4" => typeof(byte),
            "long" => typeof(long),
            _ => throw new NotImplementedException(),
        };
    }

    public bool IsEqualTo<T>(T some)
    where T: unmanaged, INumber<T>
    {
        return typeof(T) == DataType;
    }

    public bool IsEqualTo<T>(ComputeBuffer<T> _)
    where T: unmanaged, INumber<T>
    {
        return IsPointer && DataType == typeof(T);
    }
}
*/

public class Kernel : IDisposable
{
    private bool disposedValue;

    CUfunction Inner;
    object[] Args = [];
    public NDRange GlobalWork { get; set; }
    public NDRange LocalWork { get; set; }
    uint sharedMemSize;

    // TODO: вернуть waitList
    // TODO: в CUDA по-умному указывается размер работы с помощью размера сетки и 
    // размера блока, что избегает проблем с возможным заданием работы, которую 
    // нельзя поровну поделить между блоками (группами, в терминах OpenCL). Может
    // использовать такой же способ и для OpenCL?
    /// Blocking - Поставить ядро в очередь на выполнение и подождать его
    /// waitList - Список событий, которые должны быть выполнены перед выполнением ядра.
    public Event Execute(
        bool blocking = true
    )
    {
        Trace.Indent();
        var sw = Stopwatch.StartNew();

        int num = Args.Length;
        IntPtr[] array = new IntPtr[num];
        GCHandle[] array2 = new GCHandle[num];
        for (int i = 0; i < num; i++)
        {
            array2[i] = GCHandle.Alloc(Args[i], GCHandleType.Pinned);
            array[i] = array2[i].AddrOfPinnedObject();
        }

        var gridDim = new NDRange (
            GlobalWork[0] / LocalWork[0],
            GlobalWork[1] / LocalWork[1],
            GlobalWork[2] / LocalWork[2]
        );

        var ev = new Event();
        ev.Record0();
        var res = DriverAPINativeMethods.Launch.cuLaunchKernel(
            Inner,
            (uint)gridDim[0], (uint)gridDim[1], (uint)gridDim[2],
            (uint)LocalWork[0], (uint)LocalWork[1], (uint)LocalWork[2],
            sharedMemSize, CUstream.NullStream,
            array, null
        );
        if (res != CUResult.Success)
        {
            throw new Exception($"Couldn't launch a kernel, code: {res}");
        }
        ev.Record1();

        for (int j = 0; j < num; j++)
        {
            array2[j].Free();
        }

        // Trace.WriteLine($"Enqueue: {sw.ElapsedMilliseconds}");
#if CU_COLLECT_TIME
        Core.KernEvents.Add(ev);
#endif
        if (blocking)
        {
            sw.Restart();
            ev.Wait();
            // Trace.WriteLine($"Wait: {sw.ElapsedMilliseconds}");
        }
        Trace.Unindent();
        return ev;
    }

    public void SetArg<T>(
        uint idx,
        T arg)
    where T : unmanaged, INumber<T>
    {
        /*
        var info = GetArgInfo(idx);
        if (!info.IsEqualTo(arg))
        {
            throw new ArgumentException($"Expected \"{info.TypeName}\", got \"{typeof(T)}\"");
        }
        */

        if (Args.Length <= idx)
        {
            Array.Resize(ref Args, (int)(idx+1));
        }

        Args[idx] = arg;
    }

    public void SetArg<T>(
        uint idx,
        ComputeBuffer<T> mem)
    where T : unmanaged, INumber<T>
    {
        /*
        var info = GetArgInfo(idx);
        if (!info.IsEqualTo(mem))
        {
            throw new ArgumentException($"Expected \"{info.TypeName}\", got \"{typeof(T)}*\"");
        }
        */

        if (Args.Length <= idx)
        {
            Array.Resize(ref Args, (int)(idx+1));
        }

        Args[idx] = mem._deviceBuffer!;
    }

    /* NOTE: local arguments are probably not supported by CUDA
    public void SetSize<T>(
        uint idx,
        nuint sz)
    where T : unmanaged
    {
        var info = GetArgInfo(idx);
        if (!info.IsEqualTo(sz))
        {
            throw new ArgumentException($"Expected \"{info.TypeName}\", got \"{typeof(T)}\"");
        }

        Inner.SetSize<T>(idx, sz);
    }
    */

    /* NOTE: arg info might not be supported by CUDA
    public ArgInfo GetArgInfo(uint arg_index)
    {
        var name = Inner.GetArgTypeName(arg_index);
        var qual = Inner.GetArgAddressQualifier(arg_index);
        return new ArgInfo(name, qual);
    }
    */

    internal Kernel(CUfunction kernel, NDRange globalWork, NDRange localWork)
    {
        Inner = kernel;
        GlobalWork = globalWork;
        LocalWork = localWork;

        //res = DriverAPINativeMethods.FunctionManagement.cuFuncGetAttribute(ref sharedSizeBytes, CUFunctionAttribute.SharedSizeBytes, _function);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            //Inner.Dispose();
            disposedValue = true;
        }
    }

    ~Kernel()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
