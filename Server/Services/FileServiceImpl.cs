using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using FileStream;

namespace Server
{
    public class FileServiceImpl : FileService.FileServiceBase
    {
        private readonly ILogger<FileServiceImpl> _logger;
        public FileServiceImpl(ILogger<FileServiceImpl> logger)
        {
            _logger = logger;
        }

        public override async Task<UploadResponse> Upload(IAsyncStreamReader<UploadRequest> requestStream, ServerCallContext context)
        {
            var fileName = "N/A";
            var chunkId = 0;
            var totalSize = 0;

            await foreach (var request in requestStream.ReadAllAsync())
            {
                if (request.TypeCase == UploadRequest.TypeOneofCase.Meta)
                {
                    fileName = request.Meta.Path;
                    _logger.LogInformation($"Start uploading file {request.Meta.Path}");
                } else if (request.TypeCase == UploadRequest.TypeOneofCase.Chunk)
                {
                    _logger.LogInformation($"Receiving chunk {chunkId}: {request.Chunk.Length} bytes");
                    chunkId ++;
                    totalSize += request.Chunk.Length;
                } else
                {
                    // This should never happend
                    _logger.LogInformation($"Invalid request: {request.ToString()}");
                }
            }

            _logger.LogInformation($"{fileName} upload done: chunks={chunkId}; size={totalSize}");

            return new UploadResponse();
        }
    }
}
