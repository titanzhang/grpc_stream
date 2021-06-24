using System;
using System.Threading.Tasks;
using System.IO;
using Grpc.Net.Client;
using FileStream;
using Google.Protobuf;
using Grpc.Core;

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
            using var channel = GrpcChannel.ForAddress("http://localhost:5000");
            var client = new FileService.FileServiceClient(channel);

            // Call the service via stream
            await UploadFile(client, filePath, key, 2 * 1024 * 1024);
        }

        static async Task UploadFile(FileService.FileServiceClient client, string path, string key, int chunkSize = 512)
        {
            var fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
            {
                Console.WriteLine($"File not exist: {fullPath}");
                return;
            }

            // Open the file for read
            using System.IO.FileStream inputStream = File.OpenRead(fullPath);
            var metadata = new FileMeta { Key = key };
            byte[] buffer = new byte[chunkSize];

            // Open the rpc stream
            var grpcStream = new RPCUploadStream(client);

            // 1st request to send file metadata
            await grpcStream.Begin(metadata);

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
            var response = await grpcStream.End();

            Console.WriteLine($"Response: {response.ToString()}");
        }
    }

    class RPCUploadStream
    {
        private FileService.FileServiceClient _client;
        private AsyncClientStreamingCall<UploadRequest, UploadResponse> _rpcCall;

        public RPCUploadStream(FileService.FileServiceClient client)
        {
            _client = client;
        }

        public async Task Begin(FileMeta metadata)
        {
            _rpcCall = _client.Upload();
            await _rpcCall.RequestStream.WriteAsync(new UploadRequest
            {
                Meta = metadata
            });
        }

        public async Task Write(byte[] bytes, int size)
        {
            var chunk = ByteString.CopyFrom(bytes, 0, size);
            await _rpcCall.RequestStream.WriteAsync(new UploadRequest
            {
                Chunk = chunk
            });
        }

        public async Task<UploadResponse> End()
        {
            await _rpcCall.RequestStream.CompleteAsync();
            return await _rpcCall;
        }
    }
}
