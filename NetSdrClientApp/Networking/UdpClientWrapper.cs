using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NetSdrClientApp.Networking
{
    public sealed partial class UdpClientWrapper : IUdpClient, IDisposable
    {
        private readonly IPEndPoint _localEndPoint;
        private CancellationTokenSource? _cts;
        private UdpClient? _udpClient;
        private bool _disposed;

        public event EventHandler<byte[]>? MessageReceived;

        public UdpClientWrapper(int port)
        {
            _localEndPoint = new IPEndPoint(IPAddress.Any, port);
        }

        public async Task StartListeningAsync()
        {
            ThrowIfDisposed();

            _cts = new CancellationTokenSource();
            Console.WriteLine("Start listening for UDP messages...");

            try
            {
                _udpClient = new UdpClient(_localEndPoint);

                while (!_cts.Token.IsCancellationRequested)
                {
                    UdpReceiveResult result = await _udpClient.ReceiveAsync(_cts.Token);
                    MessageReceived?.Invoke(this, result.Buffer);

                    Console.WriteLine($"Received from {result.RemoteEndPoint}");
                }
            }
            catch (OperationCanceledException)
            {
                // Expected due to StopListening or Exit
            }
            catch (ObjectDisposedException)
            {
                // Shutdown race condition ignored
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error receiving message: {ex.Message}");
            }
        }

        private void StopInternal()
        {
            try
            {
                _cts?.Cancel();
                _udpClient?.Close();
                _cts?.Dispose();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed in parallel shutdown
            }
            finally
            {
                _udpClient = null;
                _cts = null;
                Console.WriteLine("Stopped listening for UDP messages.");
            }
        }

        public void StopListening() => StopInternal();

        public void Exit() => StopInternal();

        public override int GetHashCode()
        {
            var payload = $"{nameof(UdpClientWrapper)}|{_localEndPoint.Address}|{_localEndPoint.Port}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return BitConverter.ToInt32(hash, 0);
        }

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(this, obj))
                return true;

            if (obj is not UdpClientWrapper other)
                return false;

            return _localEndPoint.Address.Equals(other._localEndPoint.Address)
                && _localEndPoint.Port == other._localEndPoint.Port;
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, nameof(UdpClientWrapper));
        }
    }

    public sealed partial class UdpClientWrapper
    {
        private void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            _disposed = true;

            if (disposing)
            {
                StopInternal();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
