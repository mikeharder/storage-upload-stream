using Azure.Core.Diagnostics;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using System;
using System.IO;
using System.Reflection.Metadata;
using System.Threading.Tasks;

namespace UploadStream
{
    class Program
    {
        private static readonly int _iterations = GetEnv("ITERATIONS", 1);
        private static readonly int _uploadSize = GetEnv("UPLOAD_SIZE", 9) * 1024 * 1024;
        private static readonly int _bufferSize = GetEnv("BUFFER_SIZE", 4) * 1024 * 1024;
        private static readonly int _maxConcurrency = GetEnv("MAX_CONCURRENCY", 1);

        static async Task Main(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Environment variable STORAGE_CONNECTION_STRING is not set");
            }

            Log($"ITERATIONS: {_iterations}");
            Log($"UPLOAD_SIZE: {_uploadSize}");
            Log($"BUFFER_SIZE: {_bufferSize}");
            Log($"MAX_CONCURRENCY: {_maxConcurrency}");

            // Enable SDK logging
            using AzureEventSourceListener listener = AzureEventSourceListener.CreateConsoleLogger();

            var containerName = $"container{DateTime.Now.Ticks}";            
            var containerClient = new BlobContainerClient(connectionString, containerName);

            Log($"Creating container {containerName}");
            await containerClient.CreateAsync();
            Log($"Created container {containerName}");

            var randomBuffer = new byte[_uploadSize];
            new Random(0).NextBytes(randomBuffer);
            var randomStream = new NonSeekableMemoryStream(randomBuffer);

            for (var i=0; i < _iterations; i++)
            {
                Log($"Iteration {i}");

                var blobName = $"blob{DateTime.Now.Ticks}";
                var blobClient = containerClient.GetBlobClient(blobName);

                randomStream.Seek(0, SeekOrigin.Begin);

                Log($"Uploading blob {blobName}");
                await blobClient.UploadAsync(randomStream, transferOptions: new StorageTransferOptions()
                {
                    MaximumConcurrency = _maxConcurrency,
                    MaximumTransferLength = _bufferSize
                });
                Log($"Uploaded blob {blobName}");
            }

            Log($"Deleting container {containerName}");
            await containerClient.DeleteAsync();
            Log($"Deleted container {containerName}");
        }

        private static int GetEnv(string variable, int defaultValue)
        {
            var envString = Environment.GetEnvironmentVariable(variable);
            return string.IsNullOrEmpty(envString) ? defaultValue : int.Parse(envString);
        }

        private static void Log(object value)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {value}");
        }

        private class NonSeekableMemoryStream : MemoryStream {
            public NonSeekableMemoryStream(byte[] buffer) : base(buffer) { }

            // Forces BlobClient.UploadAsync() to upload in blocks regardless of size
            public override bool CanSeek => false;
        }

    }
}
