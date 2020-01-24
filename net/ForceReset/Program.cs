using System;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ForceReset
{
    class Program
    {
        private const int _port = 443;
        private const int _bufferSize = 16384;

        static void Main(string[] args)
        {
            var connectionString = Environment.GetEnvironmentVariable("STORAGE_CONNECTION_STRING");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("Environment variable STORAGE_CONNECTION_STRING is not set");
            }

            var accountName = Regex.Match(connectionString, "AccountName=([^;]*)").Groups[1].Value;
            var endpointSuffix = Regex.Match(connectionString, "EndpointSuffix=([^;]*)").Groups[1].Value;

            var host = $"{accountName}.blob.{endpointSuffix}";

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);

            Log($"Connecting to {host}:{_port}...");
            socket.Connect(host, _port);
            Log($"Connected");

            var buffer = new byte[_bufferSize];

            Log($"Receiving up to {_bufferSize} bytes...");
            var received = socket.Receive(buffer);
            Log($"Received {received} bytes");
        }
        
        private static void Log(string value)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {value}");
        }
    }
}
