using Azure.Core.Diagnostics;
using Azure.Core.Pipeline;
using Azure.Storage;
using Azure.Storage.Blobs;
using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace UploadStream
{
    class Program
    {
        private static readonly int _iterations = GetEnv("ITERATIONS", 1);
        private static readonly int _uploadSize = GetEnv("UPLOAD_SIZE", 9) * 1024 * 1024;
        private static readonly int _bufferSize = GetEnv("BUFFER_SIZE", 4) * 1024 * 1024;
        private static readonly int _maxConcurrency = GetEnv("MAX_CONCURRENCY", 1);

        private static readonly TimeSpan _httpClientTimeout = TimeSpan.FromSeconds(50);

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

            // Enable SDK logging (with timestamps)
            using var azureListener = new AzureEventSourceListener(
                (eventData, text) => Log(String.Format("[{1}] {0}: {2}", eventData.EventSource.Name, eventData.Level, text)),
                EventLevel.Verbose);

            // Enable System.Net logging
            using var httpListener = new LogEventListener("Microsoft-System-Net-Http");
            using var socketsListener = new LogEventListener("Microsoft-System-Net-Sockets");

            var containerName = $"container{DateTime.Now.Ticks}";

            // Test custom transport with shorter timeout
            var containerClient = new BlobContainerClient(connectionString, containerName, new BlobClientOptions()
            {
                Transport = new HttpClientTransport(new HttpClient() { Timeout = _httpClientTimeout })
            });

            Log($"Creating container {containerName}");
            await containerClient.CreateAsync();
            Log($"Created container {containerName}");

            var randomBuffer = new byte[_uploadSize];
            new Random(0).NextBytes(randomBuffer);
            var randomStream = new NonSeekableMemoryStream(randomBuffer);

            for (var i = 0; i < _iterations; i++)
            {
                try
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
                catch (Exception e)
                {
                    Log(e);
                }
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

        private class NonSeekableMemoryStream : MemoryStream
        {
            public NonSeekableMemoryStream(byte[] buffer) : base(buffer) { }

            // Forces BlobClient.UploadAsync() to upload in blocks regardless of size
            public override bool CanSeek => false;
        }

        private class LogEventListener : EventListener
        {
            private readonly string _name;
            private readonly EventLevel _eventLevel;
            private readonly EventKeywords _eventKeywords;

            public LogEventListener(string name, EventLevel eventLevel = EventLevel.LogAlways, EventKeywords eventKeywords = EventKeywords.All)
            {
                _name = name;
                _eventLevel = eventLevel;
                _eventKeywords = eventKeywords;

                foreach (var source in EventSource.GetSources())
                {
                    OnEventSourceCreated(source);
                }
            }

            protected override void OnEventSourceCreated(EventSource eventSource)
            {
                base.OnEventSourceCreated(eventSource);

                if (eventSource.Name.Equals(_name, StringComparison.OrdinalIgnoreCase))
                {
                    Log($"Adding event listener for {eventSource.Name} with level {_eventLevel} and keywords {_eventKeywords}");
                    EnableEvents(eventSource, _eventLevel, _eventKeywords);
                }
            }

            protected override void OnEventWritten(EventWrittenEventArgs eventData)
            {
                base.OnEventWritten(eventData);

                Log($"{eventData.EventSource.Name}_{eventData.EventName}: {String.Join(',', eventData.Payload)}");
            }
        }
    }
}
