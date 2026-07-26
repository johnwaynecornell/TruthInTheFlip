using System.Reflection;
using System.Runtime.InteropServices;

namespace TruthInTheFlip.Format;

public class QuantisExtractor : IDisposable
{
    public class Imports
    {
        // 1. Define a delegate that perfectly matches your C function pointer signature
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate int GetRandomDelegate(IntPtr buffer, UIntPtr length);

        // 2. Import the native Extractor functions
        [DllImport("Quantis_Extensions", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        public static extern int QuantisExtractorInitializeMatrix(
            string matrixFilename,
            ref IntPtr extractorMatrix,
            ushort matrixSizeIn,
            ushort matrixSizeOut);

        [DllImport("Quantis_Extensions", CallingConvention = CallingConvention.Cdecl)]
        public static extern void QuantisExtractorGetDataFromBuffer(
            byte[] inputBuffer,
            byte[] outputBuffer,
            IntPtr extractorMatrix,
            uint numberOfBytesAfterExtraction);

        [DllImport("Quantis_Extensions", CallingConvention = CallingConvention.Cdecl)]
        public static extern void QuantisExtractorUninitializeMatrix(ref IntPtr extractorMatrix);
    }
    
    public IntPtr extractorMatrix = IntPtr.Zero;
    
    public string FileName = "default_idq_matrix.dat";
    
    public byte[] RawBuffer = new byte[3072];
    public byte[] ExtractedBuffer = new byte[1024];
    public nint extractedPosition = nint.MaxValue;
    
    private string? _tempMatrixPath = null;
    
    protected void RefillExtractedBuffer()
    {
        RandomSource(RawBuffer);
        Imports.QuantisExtractorGetDataFromBuffer(RawBuffer, ExtractedBuffer, extractorMatrix, (uint)ExtractedBuffer.Length);
        extractedPosition = 0;
    }
    
    public string FindMatrixFile()
    {
        // Strategy 1: Check application local folder first (Allows local overrides during dev/testing)
        string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);
        if (File.Exists(localPath)) 
            return localPath;

        // Strategy 2: Check current working directory as a secondary local fallback
        if (File.Exists(FileName)) 
            return FileName;

        // Strategy 3: Always extract the embedded, perfectly-matched resource
        return ExtractEmbeddedMatrix();
    }
    
    private string ExtractEmbeddedMatrix()
    {
        string resourceName = $"TruthInTheFlip.Format.{FileName}";
        var assembly = Assembly.GetExecutingAssembly();

        var streams = assembly.GetManifestResourceNames();
        
        using Stream? stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
        {
            throw new FileNotFoundException($"Could not resolve {FileName} via system paths, and the embedded resource '{resourceName}' was not found.");
        }

        // Generate a unique temp file name using a Guid to prevent collision
        string randomFileName = $"{Guid.NewGuid()}_{FileName}";
        _tempMatrixPath = Path.Combine(Path.GetTempPath(), randomFileName);
        
        using FileStream fileStream = new FileStream(_tempMatrixPath, FileMode.Create, FileAccess.Write);
        stream.CopyTo(fileStream);

        return _tempMatrixPath;
    }

    public bool InitializeDefaultMatrix()
    {
        int status = -1;

        if (extractorMatrix == 0)
        {
            // Initialize Matrix (update with your actual matrix bit sizes)
            try
            {

                status = Imports.QuantisExtractorInitializeMatrix(FindMatrixFile(), ref extractorMatrix, 192, 64);
            }
            catch (DllNotFoundException e)
            {
                Console.Error.WriteLine(
                    "Quantis_Extensions library not found. Please ensure it is installed and accessible.");
                Console.Error.WriteLine(e.Message);
                return false;
            }

            if (status != 0)
            {
                Console.Error.WriteLine($"Matrix initialization failed: {status}");
                return false;
            }
        }
        
        return true;
    }

    public void UninitializeMatrix()
    {
        if (extractorMatrix != IntPtr.Zero)
        {
            Imports.QuantisExtractorUninitializeMatrix(ref extractorMatrix);
            extractorMatrix = IntPtr.Zero;
        }

        // Clean up the temporary extracted matrix file if we created one
        if (_tempMatrixPath != null)
        {
            try
            {
                if (File.Exists(_tempMatrixPath))
                {
                    File.Delete(_tempMatrixPath);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: Failed to delete temporary matrix file at {_tempMatrixPath}. {ex.Message}");
            }
            finally
            {
                // Null it out so subsequent calls to Dispose() are safe
                _tempMatrixPath = null; 
            }
        }
    }
    public Action<byte[]> RandomSource { get; set; } =
        bytes => System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
    
    private readonly object _extractionLock = new object();

    public void FillBuffer(byte[] buffer)
    {
        lock (_extractionLock)
        {
            for (nint i = 0; i < buffer.Length; i++)
            {
                if (extractedPosition >= ExtractedBuffer.Length)
                {
                    RefillExtractedBuffer();
                }
                buffer[i] = ExtractedBuffer[extractedPosition++];
            }
        }
    }

    public void Dispose()
    {
        UninitializeMatrix();
    }
}