using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EchoServer
{
    public class EchoServer
    {
        private readonly int _port;
        private TcpListener? _listener; // Змінено на nullable
        private CancellationTokenSource _cancellationTokenSource;

        //constuctor
        public EchoServer(int port)
        {
            _port = port;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public async Task StartAsync()
        {
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            Console.WriteLine($"Server started on port {_port}.");

            while (!_cancellationTokenSource.Token.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    Console.WriteLine("Client connected.");

                    // Оновлено: передаємо NetworkStream для обробки
                    _ = Task.Run(() => HandleClientAsync(client.GetStream(), _cancellationTokenSource.Token));
                }
                catch (ObjectDisposedException)
                {
                    // Listener has been closed
                    break;
                }
            }

            Console.WriteLine("Server shutdown.");
        }

        public void Stop()
        {
            _cancellationTokenSource.Cancel();
            _listener?.Stop(); // Використовуємо ?.
        }

        // Змінено на PUBLIC та приймає абстракцію Stream для тестування
        public async Task HandleClientAsync(Stream stream, CancellationToken token)
        {
            using (stream) // Використовуємо наданий Stream
            {
                byte[] buffer = new byte[1024];
                int bytesRead;

                try
                {
                    while (!token.IsCancellationRequested && (bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, token)) != 0)
                    {
                        Console.WriteLine($"Received: {bytesRead} bytes.");

                        // Echo logic: send received data back
                        await stream.WriteAsync(buffer, 0, bytesRead, token); 
                        Console.WriteLine($"Sent: {bytesRead} bytes.");
                    }
                }
                catch (IOException ex) when (ex.InnerException is SocketException se && se.SocketErrorCode == SocketError.ConnectionReset)
                {
                    Console.WriteLine("Client disconnected forcefully.");
                }
                catch (OperationCanceledException)
                {
                    // Токен скасовано
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error handling client: {ex.Message}");
                }
            }
            Console.WriteLine("Client disconnected.");
        }
    }

    // Решта коду класу UdpTimedSender без змін
    // ...
}
