using System;
using System.Threading.Tasks;
using System.IO;
using Grpc.Net.Client;
using FileStream;
using Google.Protobuf;

namespace Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Specify a file");
                return;
            }

            // Create gRPC connection and client
            using var channel = GrpcChannel.ForAddress("http://localhost:5000");
            var client = new FileService.FileServiceClient(channel);

            // Call the service via stream
            await UploadFile(client, args[0], 512);
        }

        static async Task UploadFile(FileService.FileServiceClient client, string path, int chunkSize)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"File not exist: {fullPath}");
                return;
            }

            using System.IO.FileStream fs = File.OpenRead(fullPath);
            await UploadFromStream(client, new FileMeta { Path = fullPath }, fs, 512);
        }

        static async Task UploadFromStream(FileService.FileServiceClient client, FileMeta metadata, Stream inputStream, int chunkSize)
        {
            byte[] buffer = new byte[chunkSize];

            // Call the service via stream
            using (var call = client.Upload())
            {
                // 1st request to send file metadata
                await call.RequestStream.WriteAsync(new UploadRequest
                {
                    Meta = metadata
                });

                // rest requests to send file chunks
                while (true)
                {
                    var size = inputStream.Read(buffer, 0, buffer.Length);
                    if (size <= 0)
                    {
                        break;
                    }

                    var chunk = ByteString.CopyFrom(buffer, 0, size);
                    await call.RequestStream.WriteAsync(new UploadRequest
                    {
                        Chunk = chunk
                    });
                }

                await call.RequestStream.CompleteAsync();
                var response = await call;
                Console.WriteLine($"Response: {response.ToString()}");
            }
        }
    }


}
