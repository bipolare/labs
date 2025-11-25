// ITcpListener.cs
// -----------------------------
// Abstraction over TcpListener.
// Makes the server independent from system sockets,
// which allows unit testing without using actual networking.

using System.Net.Sockets;
using System.Threading.Tasks;

namespace EchoTspServer
{
    // Абстракція над TcpListener, щоб EchoServer можна було мокати в тестах.
    public interface ITcpListener
    {
        void Start();
        void Stop();
        Task<TcpClient> AcceptTcpClientAsync();
    }
}
