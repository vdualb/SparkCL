using ManagedCuda.BasicTypes;
using ManagedCuda.NVRTC;
using static ManagedCuda.DriverAPINativeMethods.ModuleManagement;

using SparkCompute;

namespace SparkCU;

public class ComputeProgram : IDisposable
{
    private bool disposedValue;
    
    CUmodule program;

    //public ComputeProgram(string fileName)
    //{
    //    program = Program.FromFilename(Core.context!, Core.clDevice!, fileName);
    //}

    ComputeProgram(CUmodule _program)
    {
        program = _program;
    }

    public static ComputeProgram FromChoice(ProgramSource src)
    {
        if (src.SourceCuda != null)
        {

        }
        throw new NotImplementedException();
    }

    public static ComputeProgram FromString(string source)
    {
        using var rtc = new CudaRuntimeCompiler(source, null);
        string log;
        try
        {
            rtc.Compile([
                "--gpu-architecture=compute_20",
            ]);
        }
        catch (NVRTCException)
        {
            log = rtc.GetLogAsString();
            Console.WriteLine($"Compile error: " + log);
            throw;
        }
        log = rtc.GetLogAsString();
        Console.WriteLine($"Compile log: " + (log.Length > 0 ? log : "<empty>"));

        byte[] bin = rtc.GetPTX();

        CUmodule hcuModule = new();
        var res = cuModuleLoadData(ref hcuModule, bin);

        if (res != CUResult.Success)
        {
            throw new Exception($"Couldn't create CUModule from .cu source, code: {res}");
        }

        return new ComputeProgram(hcuModule);
    }

    static ComputeProgram FromCudaSource(string fileName, string prependSource = "")
    {
        string cuStr = prependSource + "\n" + File.ReadAllText(fileName);

        throw new NotImplementedException();
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="prependSource">Prepend given string to program source. Might be useful for adding '#define's. Additional newline is inserted after <paramref name="prependSource"/>. </param>
    /// <returns></returns>
    public static ComputeProgram FromFilename(string fileName, string prependSource = "")
    {
        var contents = prependSource + File.ReadAllText("opencl_to_cuda.h") + File.ReadAllText(fileName);
        var rtc = new CudaRuntimeCompiler(contents, fileName);
        try
        {
            rtc.Compile([]);
        }
        catch (NVRTCException)
        {
            var log = rtc.GetLogAsString();
            Console.WriteLine($"Compile error: " + log);
            Console.WriteLine($"Program source: ```\n{contents}\n```");
            throw;
        }

        byte[] bin = rtc.GetPTX();

        var file_ptx = $"{fileName}.ptx";
        var file_cubin = $"{fileName}.cubin";

        /*
        if (File.Exists(file_cubin))
        {
            Console.WriteLine($"Using local cubin");
            bin = File.ReadAllBytes(file_cubin);
        }
        else if (File.Exists(file_ptx))
        {
            Console.WriteLine($"Using local ptx");
            bin = File.ReadAllBytes(file_ptx);
        }
        else
        {
            throw new Exception($"Couldn't find `{fileName}`");
        }
        */

        CUmodule hcuModule = new();
        var res = cuModuleLoadData(ref hcuModule, bin);

        if (res != CUResult.Success)
        {
            throw new Exception($"Couldn't create CUModule from filename `{fileName}`, code: {res}");
        }

        return new ComputeProgram(hcuModule);
    }

    // TODO: cuda doesn't seem to provide API to dump ptx code.
    // but future unified compute interface probably doesn't require it anyway
    //public byte[][] GetBinaries()
    //{
    //    cumodule
    //    return program.GetBinaries();
    //}

    public SparkCU.Kernel GetKernel(string kernelName, NDRange globalWork, NDRange localWork)
    {
        CUfunction func = new();
        var res = cuModuleGetFunction(ref func, program, kernelName);

        if (res != CUResult.Success)
        {
            throw new Exception($"Couldn't get kernel '{kernelName}' from cuda program: {res}");
        }

        return new Kernel(func, globalWork, localWork);
    }
    
    //public byte[][] GetBinaries()
    //{
    //    return program.GetBinaries();
    //}
    
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            //program.Dispose();
            disposedValue = true;
        }
    }

    ~ComputeProgram()
    {
        Dispose(disposing: false);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
