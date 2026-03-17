using ManagedCuda.BasicTypes;
using System.Reflection;
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

    /// <summary>
    /// 
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="prependSource">Prepend given string to program source. Might be useful for adding '#define's. Additional newline is inserted after <paramref name="prependSource"/>. </param>
    /// <returns></returns>
    public static ComputeProgram FromFilename(string fileName, string prependSource = "")
    {
        using var sr = new StreamReader(fileName);
        string clStr = prependSource + "\n" + sr.ReadToEnd();

        var program = OCLHelper.Program.CreateWithSource(Core.clContext!, [clStr]);

        var options = "-cl-kernel-arg-info"u8;

        try
        {
            program.Build(options);
        } catch (Exception)
        {
            string? build_log = program.GetBuildLog(Core.clDevice!);

            //Console.WriteLine("Error in kernel: ");
            Console.WriteLine("=============== OpenCL Program Build Info ================");
            Console.WriteLine(build_log);
            Console.WriteLine("==========================================================");

            throw;
        }

        var bins = program.GetBinaries();
        CUmodule hcuModule = new();
        var res = cuModuleLoadDataEx(ref hcuModule, bins[0], 0, null, null);

        if (res != CUResult.Success)
        {
            throw new Exception($"Couldn't create CUModule: {res}");
        }

        return new ComputeProgram(hcuModule);
    }

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
