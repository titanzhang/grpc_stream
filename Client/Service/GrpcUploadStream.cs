using System.Threading.Tasks;
using FileStream;
using Google.Protobuf;
using Grpc.Core;

namespace Client.Service
{
    public class GrpcUploadStream
    {
        private AsyncClientStreamingCall<UploadRequest, UploadResponse> _rpcCall;

        public async Task Begin(FileService.FileServiceClient client, FileMeta metadata)
        {
            _rpcCall = client.Upload();
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
