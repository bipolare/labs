// IClientHandler.cs
// -----------------------------
// Defines how a connected client should be processed.
// Allows testing client communication logic in isolation
// without running the entire server.

using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace EchoTspServer
{
    // Інтерфейс обробника клієнта.
    // Дозволяє окремо тестувати логіку роботи з клієнтом.
    public interface IClientHandler
    {
        Task HandleClientAsync(TcpClient client, CancellationToken token);
    }
}
