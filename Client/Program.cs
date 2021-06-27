using System;
using System.Threading.Tasks;
using System.IO;
using FileStream;
using Client.Service;

namespace Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("cmd <file path> <key>");
                return;
            }
            var filePath = args[0];

            if (args.Length < 2)
            {
                Console.WriteLine($"cmd \"{filePath}\" <key>");
                return;
            }
            var key = args[1];

            // Create gRPC connection and client
            var fs = GrpcFileOperation.ForAddress("http://localhost:5000");

            // Call the service via stream
            try
            {
                await UploadFile(fs, filePath, key, 2 * 1024 * 1024);
            } catch(Exception e)
            {
                Console.WriteLine(e);
            } finally
            {
                // Cleanup
                await fs.Close();
            }
        }

        static async Task UploadFile(GrpcFileOperation fs, string path, string key, int chunkSize = 512)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"File not exist: {fullPath}");
                return;
            }

            // Open the file for read
            using System.IO.FileStream inputStream = File.OpenRead(fullPath);
            byte[] buffer = new byte[chunkSize];

            // 1st request to send file metadata
            var grpcStream = await fs.BeginUpload(key);

            // rest requests to send file chunks
            while (true)
            {
                var size = inputStream.Read(buffer, 0, buffer.Length);
                if (size <= 0)
                {
                    break;
                }

                await grpcStream.Write(buffer, size);
            }

            // Done
            var fileId = await fs.EndUpload();

            Console.WriteLine($"Response: {fileId}");
        }
    }
}
