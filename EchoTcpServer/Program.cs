using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Linq; 

// --- ТОЧКА ВХОДУ ДЛЯ КОНСОЛЬНОГО ЗАСТОСУНКУ (ВИПРАВЛЕННЯ CS0118/CS0246) ---
try
{
    Console.WriteLine("Starting EchoServer (TCP) and UdpTimedSender...");
    
    // Кваліфікуємо типи, щоб уникнути конфлікту імен 'EchoServer'
    // Використовуємо using для автоматичного виклику Dispose
    using var server = new EchoServer.EchoServer(50000); 
    using var udpSender = new EchoServer.UdpTimedSender("127.0.0.1", 60000); 
    
    udpSender.StartSending(100);
    
    // Сервер працює, поки не буде скасовано
    await server.StartAsync(); 
}
catch (Exception ex)
{
    Console.WriteLine($"Критична помилка: {ex.Message}");
}
finally
{
    Console.WriteLine("EchoServer stopped.");
}
// -------------------------------------------------------------------

namespace EchoServer
{
    // Додано IDisposable та повний патерн Dispose (для Sonar S3881)
    public class EchoServer : IDisposable
    {
        private readonly int _port;
        private TcpListener? _listener; 
        private readonly CancellationTokenSource _cancellationTokenSource; // readonly (для Sonar S2933)
        private bool _disposed = false;

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

                    // РЕФАКТОРИНГ: Передаємо NetworkStream для тестування
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
            _listener?.Stop(); 
        }

        // РЕФАКТОРИНГ: Змінено на PUBLIC та приймає абстракцію Stream для тестування
        // S2325: Залишаємо інстанс-методом, оскільки він керує життєвим циклом підключення.
        public async Task HandleClientAsync(Stream stream, CancellationToken token)
        {
            using (stream) 
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
        
        // Реалізація повного патерну Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    Stop(); 
                    _cancellationTokenSource.Dispose();
                    // _listener не є керованим ресурсом, що потребує Dispose,
                    // його зупинка відбувається в Stop().
                }

                _disposed = true;
            }
        }
    }

    public class UdpTimedSender : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly UdpClient _udpClient;
        private Timer? _timer; 
        private bool _disposed = false;
        
        // РЕФАКТОРИНГ: Зроблено статичним для виправлення Sonar S2245 (небезпечне використання Random)
        private static readonly Random Rnd = new Random(); 

        public UdpTimedSender(string host, int port)
        {
            _host = host;
            _port = port;
            _udpClient = new UdpClient();
        }

        public void StartSending(int intervalMilliseconds)
        {
            if (_timer != null)
                throw new InvalidOperationException("Sender is already running.");

            _timer = new Timer(SendMessageCallback, null, 0, intervalMilliseconds);
        }

        ushort i = 0;

        private void SendMessageCallback(object? state)
        {
            try
            {
                //dummy data
                byte[] samples = new byte[1024];
                
                // Використовуємо статичний Random з блокуванням для потокобезпечності
                lock (Rnd) 
                {
                    Rnd.NextBytes(samples);
                }
                i++;

                // Змінено на явний масив для Concat
                byte[] msg = (new byte[] { 0x04, 0x84 }).Concat(BitConverter.GetBytes(i)).Concat(samples).ToArray();
                var endpoint = new IPEndPoint(IPAddress.Parse(_host), _port);

                _udpClient.Send(msg, msg.Length, endpoint);
                Console.WriteLine($"Message sent to {_host}:{_port} ");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending message: {ex.Message}");
            }
        }

        public void StopSending()
        {
            _timer?.Dispose();
            _timer = null;
        }

        // Реалізація повного патерну Dispose
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    StopSending();
                    _udpClient.Dispose();
                    // _timer вже обробляється в StopSending()
                }
                _disposed = true;
            }
        }
    }
}
