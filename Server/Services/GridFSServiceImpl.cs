using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using FileStream;
using MongoDB.Driver;
using MongoDB.Driver.GridFS;
using MongoDB.Bson;

namespace Server.Service
{
    public class GridFSServiceImpl: FileService.FileServiceBase
    {
        private readonly ILogger<SimpleFileServiceImpl> _logger;
        public GridFSServiceImpl(ILogger<SimpleFileServiceImpl> logger)
        {
            _logger = logger;
        }

        public override async Task<UploadResponse> Upload(IAsyncStreamReader<UploadRequest> requestStream, ServerCallContext context)
        {
            var fileKey = "N/A";
            var chunkId = 0;
            var totalSize = 0;

            var fs = GridFSDriver.Create();
            GridFSUploadStream uploadStream = null;
            var response = new UploadResponse();

            try
            {
                await foreach (var request in requestStream.ReadAllAsync())
                {
                    if (request.TypeCase == UploadRequest.TypeOneofCase.Meta)
                    {
                        // 1st request: file metadata
                        fileKey = request.Meta.Key;
                        uploadStream = await fs.BeginUpload(fileKey);
                        _logger.LogInformation($"Start uploading file {fileKey}");
                    }
                    else if (request.TypeCase == UploadRequest.TypeOneofCase.Chunk)
                    {
                        // rest requests: chunk of file content
                        var bytes = request.Chunk.ToByteArray();
                        await uploadStream.WriteAsync(bytes, 0, bytes.Length);
                        _logger.LogInformation($"Receiving chunk {chunkId}: {request.Chunk.Length} bytes");
                        chunkId++;
                        totalSize += request.Chunk.Length;
                    }
                    else
                    {
                        // This should never happend
                        _logger.LogInformation($"Invalid request: {request.ToString()}");
                    }
                }
            }
            catch(Exception e)
            {
                await fs.EndUpload(uploadStream);

                response.ErrorMsg = e.ToString();
                _logger.LogError("Error: {}", e);
                return response;
            }

            var objectId = await fs.EndUpload(uploadStream);
            response.FileId = objectId.ToString();

            _logger.LogInformation($"{fileKey} upload done: chunks={chunkId}; size={totalSize}");

            return response;
        }
    }

    class GridFSDriver
    {
        static GridFSBucket getBucket()
        {
            var serverAddress = "localhost:27017";
            var userName = "root";
            var password = "root";
            var connectionString = $"mongodb://{userName}:{password}@{serverAddress}";
            var dbName = "test";

            var client = new MongoClient(connectionString);
            var db = client.GetDatabase(dbName);

            var bucket = new GridFSBucket(db, new GridFSBucketOptions
            {
                BucketName = "fs",
                ChunkSizeBytes = 512,
                WriteConcern = WriteConcern.WMajority,
                ReadPreference = ReadPreference.Secondary
            });
            if (bucket == null)
            {
                throw new Exception("bucker is null");
            }

            return bucket;
        }

        public static GridFSOperation Create()
        {
            return new GridFSOperation
            {
                Bucket = getBucket()
            };
        }
    }

    class GridFSOperation
    {
        IGridFSBucket _bucket;
        public IGridFSBucket Bucket
        {
            get { return _bucket; }
            set { _bucket = value; }
        }

        public async Task<GridFSUploadStream> BeginUpload(string key)
        {
            var fileName = Path.GetFileName(key);

            var options = new GridFSUploadOptions
            {
                Metadata = new BsonDocument
                {
                    { "key", key }
                }
            };
            var stream = await _bucket.OpenUploadStreamAsync(fileName, options);

            return stream;
        }

        public async Task<ObjectId> EndUpload(GridFSUploadStream uploadStream)
        {
            var id = uploadStream.Id;
            await uploadStream.CloseAsync();

            return id;
        }

        public async Task<List<GridFSFileInfo>> List()
        {
            var filter = Builders<GridFSFileInfo>.Filter.Empty;
            var sort = Builders<GridFSFileInfo>.Sort.Descending(x => x.UploadDateTime);
            var options = new GridFSFindOptions
            {
                Limit = 10,
                Sort = sort
            };
            using var cursor = await _bucket.FindAsync(filter, options);
            var fileList = await cursor.ToListAsync();

            return fileList;
        }

        public async Task<List<GridFSFileInfo>> Search(string regex)
        {
            var filter = Builders<GridFSFileInfo>.Filter.Regex(x => x.Metadata["key"], new BsonRegularExpression(regex));
            var sort = Builders<GridFSFileInfo>.Sort.Descending(x => x.UploadDateTime);
            var options = new GridFSFindOptions
            {
                Limit = 10,
                Sort = sort
            };
            using var cursor = await _bucket.FindAsync(filter, options);
            var fileList = await cursor.ToListAsync();

            return fileList;
        }
    }

}
