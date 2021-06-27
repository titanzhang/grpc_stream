using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using CommandLine;
using Server.Service;
using Client.Service;
using FileStream;

namespace test
{
    using WriteFunc = Func<WriteOptions, Task<TestStats>>;

    class Program
    {
        static Dictionary<AccessMode, WriteFunc> writeFuncMap = new Dictionary<AccessMode, WriteFunc>()
        {
            { AccessMode.disk, WriteOperation.WriteDisk },
            { AccessMode.gridfs, WriteOperation.WriteGridFS },
            { AccessMode.grpc, WriteOperation.WriteGRPC },

        };

        static async Task<int> Main(string[] args)
        {
            Func<WriteOptions, Task<int>> performWrite = async opts =>
            {
                try
                {
                    var writeFunc = writeFuncMap[opts.Mode];
                    var stats = await writeFunc(opts);
                    Console.WriteLine($"Size = {stats.TotalSize} bytes");
                    Console.WriteLine($"Time = {stats.TotalTime:0.00}s");
                    Console.WriteLine($"Transfer Rate = {stats.Rate:0.00}MB/s");
                    return 0;
                } catch(Exception e)
                {
                    Console.WriteLine(e);
                    return -1;
                }
            };

            Func<ReadOptions, Task<int>> performRead = opts =>
            {
                Console.WriteLine("Read hasn't been implemented.");
                return Task.FromResult(-1);
            };

            return await Parser.Default.ParseArguments<WriteOptions, ReadOptions>(args)
                .MapResult(
                    (WriteOptions opts) => performWrite(opts),
                    (ReadOptions opts) => performRead(opts),
                    errs => Task.FromResult(1)
                );
        }

    }

    class TestStats
    {
        public long TotalTimeMS { get; set; }
        public long TotalSize { get; set; }
        public double TotalTime
        {
            private set { }
            get { return TotalTimeMS / 1000d; }
        }
        public double Rate
        {
            private set { }
            get
            {
                return (TotalSize / 1024d / 1024d) / (TotalTimeMS / 1000d);
            }
        }
    }

    class WriteOperation
    {
        public static async Task<TestStats> WriteDisk(WriteOptions opts)
        {
            return await writeAndStats(opts, async (string filePath, byte[] buffer, int totalSize) =>
            {
                var fullPath = Path.GetFullPath(filePath);
                var bufferSize = buffer.Length;
                using (var outStream = File.OpenWrite(fullPath))
                {
                    var bytesLeft = totalSize;
                    while (bytesLeft >= bufferSize)
                    {
                        await outStream.WriteAsync(buffer, 0, bufferSize);
                        bytesLeft -= bufferSize;
                    }
                    if (bytesLeft > 0)
                    {
                        await outStream.WriteAsync(buffer, 0, bytesLeft);
                    }
                }
            });
        }

        public static async Task<TestStats> WriteGridFS(WriteOptions opts)
        {
            return await writeAndStats(opts, async (string key, byte[] buffer, int totalSize) =>
            {
                var bufferSize = buffer.Length;
                var fs = GridFSDriver.Create();

                using (var uploadStream = await fs.BeginUpload(key))
                {
                    var bytesLeft = totalSize;
                    while (bytesLeft >= bufferSize)
                    {
                        await uploadStream.WriteAsync(buffer, 0, bufferSize);
                        bytesLeft -= bufferSize;
                    }
                    if (bytesLeft > 0)
                    {
                        await uploadStream.WriteAsync(buffer, 0, bytesLeft);
                    }
                }
            });
        }

        public static async Task<TestStats> WriteGRPC(WriteOptions opts)
        {
            return await writeAndStats(opts, async (string key, byte[] buffer, int totalSize) =>
            {
                var bufferSize = buffer.Length;
                var fs = GrpcFileOperation.ForAddress("http://localhost:5000");

                try
                {
                    var uploadStream = await fs.BeginUpload(key);

                    var bytesLeft = totalSize;
                    while (bytesLeft >= bufferSize)
                    {
                        await uploadStream.Write(buffer, bufferSize);
                        bytesLeft -= bufferSize;
                    }
                    if (bytesLeft > 0)
                    {
                        await uploadStream.Write(buffer, bytesLeft);
                    }

                    await fs.EndUpload();
                }
                finally
                {
                    await fs.Close();
                }
            });
        }

        private static async Task<TestStats> writeAndStats(WriteOptions opts, Func<string, byte[], int, Task> writeFileImpl)
        {
            // Prepare the writing buffer
            byte[] buffer;
            createBuffer(out buffer);

            // Write files in sequence
            var startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            for (var i = 0; i < opts.RepeatCount; i++)
            {
                await writeFileImpl(opts.Key + i, buffer, opts.FileSize);
            }
            var endTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

            // Populate stats
            var stats = new TestStats
            {
                TotalTimeMS = endTime - startTime,
                TotalSize = opts.FileSize * opts.RepeatCount
            };

            return stats;
        }

        static void createBuffer(out byte[] buffer)
        {
            var bufferSize = 128 * 1024;
            buffer = new byte[bufferSize];
            for (var i = 0; i < bufferSize; i++)
            {
                buffer[i] = ((byte)'a');
            }
        }
    }
}