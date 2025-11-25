using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    public partial class TcpClientWrapper : ITcpClient, IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private CancellationTokenSource? _cts;

        private bool _disposed;

        public bool Connected => _tcpClient != null && _tcpClient.Connected && _stream != null;

        public event EventHandler<byte[]>? MessageReceived;

        public TcpClientWrapper(string host, int port)
        {
            _host = host;
            _port = port;
        }

        public void Connect()
        {
            if (Connected)
            {
                Console.WriteLine($"Already connected to {_host}:{_port}");
                return;
            }

            _tcpClient = new TcpClient();

            try
            {
                _cts = new CancellationTokenSource();
                _tcpClient.Connect(_host, _port);
                _stream = _tcpClient.GetStream();
                Console.WriteLine($"Connected to {_host}:{_port}");
                _ = StartListeningAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to connect: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (!Connected)
            {
                Console.WriteLine("No active connection to disconnect.");
                return;
            }

            try
            {
                _cts?.Cancel();
                _stream?.Close();
                _tcpClient?.Close();
            }
            catch (ObjectDisposedException)
            {
                // Ignore dispose races
            }
            finally
            {
                _cts?.Dispose();
                _tcpClient?.Dispose();
                _stream?.Dispose();

                _cts = null;
                _tcpClient = null;
                _stream = null;

                Console.WriteLine("Disconnected.");
            }
        }

        public async Task SendMessageAsync(byte[] data)
        {
            await SendDataAsync(data);
        }

        public async Task SendMessageAsync(string str)
        {
            var data = Encoding.UTF8.GetBytes(str);
            await SendDataAsync(data);
        }

        private async Task SendDataAsync(byte[] data)
        {
            if (Connected && _stream != null && _stream.CanWrite)
            {
                Console.WriteLine($"Message sent: {BitConverter.ToString(data)}");
                await _stream.WriteAsync(data.AsMemory(), CancellationToken.None);
            }
            else
            {
                throw new InvalidOperationException("Not connected to a server.");
            }
        }

        private async Task StartListeningAsync()
        {
            if (!(Connected && _stream != null && _stream.CanRead))
            {
                throw new InvalidOperationException("Not connected to a server.");
            }

            try
            {
                Console.WriteLine($"Starting listening for incoming messages.");

                while (!_cts!.Token.IsCancellationRequested)
                {
                    byte[] buffer = new byte[8194];

                    int bytesRead = await _stream.ReadAsync(buffer.AsMemory(), _cts.Token);

                    if (bytesRead > 0)
                    {
                        MessageReceived?.Invoke(this, buffer.AsSpan(0, bytesRead).ToArray());
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
            }
            catch (ObjectDisposedException)
            {
                // Stream or token got disposed during shutdown
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in listening loop: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("Listener stopped.");
            }
        }

        // ************* DISPOSE PATTERN FIX *************

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;

            if (disposing)
            {
                Disconnect();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
