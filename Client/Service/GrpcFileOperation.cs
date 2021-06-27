using System;
using System.Threading.Tasks;
using Grpc.Net.Client;
using FileStream;

namespace Client.Service
{
    public class GrpcFileOperation
    {
        private GrpcChannel _channel;
        private FileService.FileServiceClient _client;
        private GrpcUploadStream _uploadStream = new GrpcUploadStream();

        public static GrpcFileOperation ForAddress(string serverAddress)
        {
            var channel = GrpcChannel.ForAddress(serverAddress);
            return new GrpcFileOperation(channel);
        }

        public GrpcFileOperation(GrpcChannel channel)
        {
            _channel = channel;
            _client = new FileService.FileServiceClient(channel);
        }

        public async Task<GrpcUploadStream> BeginUpload(string key)
        {
            var metadata = new FileMeta { Key = key };
            await _uploadStream.Begin(_client, metadata);
            return _uploadStream;
        }

        public async Task<string> EndUpload()
        {
            var response = await _uploadStream.End();
            if (response.ResultCase == UploadResponse.ResultOneofCase.ErrorMsg)
            {
                throw new Exception(response.ErrorMsg);
            }

            return response.FileId;
        }

        public async Task Close()
        {
            await _channel.ShutdownAsync();
            _client = null;
            _channel = null;
        }
    }
}
