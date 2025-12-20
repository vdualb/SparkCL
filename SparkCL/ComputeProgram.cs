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

    public SparkCL.Kernel GetKernel(string kernelName, NDRange globalWork, NDRange localWork)
    {
        var oclKernel = new OCLHelper.Kernel(program, kernelName);
        return new Kernel(oclKernel, globalWork, localWork);
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
