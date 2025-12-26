using OCLHelper;

namespace SparkCL;

public class ComputeProgram : IDisposable
{
    private bool disposedValue;
    
    Program program;

    public ComputeProgram(string fileName)
    {
        program = Program.FromFilename(Core.context!, Core.device!, fileName);
    }

    ComputeProgram(Program _program)
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

        var program = Program.CreateWithSource(Core.context!, [clStr]);

        var options = "-cl-kernel-arg-info"u8;

        try
        {
            program.Build(options);
        } catch (Exception)
        {
            string? build_log = program.GetBuildLog(Core.device!);

            //Console.WriteLine("Error in kernel: ");
            Console.WriteLine("=============== OpenCL Program Build Info ================");
            Console.WriteLine(build_log);
            Console.WriteLine("==========================================================");

            throw;
        }

        return new ComputeProgram(program);
    }

    public SparkCL.Kernel GetKernel(string kernelName, NDRange globalWork, NDRange localWork)
    {
        var oclKernel = new OCLHelper.Kernel(program, kernelName);
        return new Kernel(oclKernel, globalWork, localWork);
    }
    
    public byte[][] GetBinaries()
    {
        return program.GetBinaries();
    }
    
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            program.Dispose();
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
