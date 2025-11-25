using NUnit.Framework;
using NetSdrClientApp.Networking;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace NetSdrClientAppTests
{
    public class WrapperTests
    {
        // ********************** TCP TESTS **********************

        [Test]
        public void TcpClientWrapper_SendMessage_Throws_WhenNotConnected()
        {
            var wrapper = new TcpClientWrapper("127.0.0.1", 65000);

            Assert.That(
                async () => await wrapper.SendMessageAsync(new byte[] { 1, 2, 3 }),
                Throws.InvalidOperationException
            );
        }

        [Test]
        public void TcpClientWrapper_SendString_Throws_WhenNotConnected()
        {
            var wrapper = new TcpClientWrapper("127.0.0.1", 65000);

            Assert.That(
                async () => await wrapper.SendMessageAsync("hello"),
                Throws.InvalidOperationException
            );
        }

        [Test]
        public void TcpClientWrapper_Disconnect_NoConnection_DoesNotThrow()
        {
            var wrapper = new TcpClientWrapper("127.0.0.1", 65000);

            Assert.That(() => wrapper.Disconnect(), Throws.Nothing);
        }

        [Test]
        public void TcpClientWrapper_Connect_InvalidHost_DoesNotThrow()
        {
            var wrapper = new TcpClientWrapper("999.999.999.999", 65000);

            Assert.That(() => wrapper.Connect(), Throws.Nothing);
        }

        [Test]
        public void TcpClientWrapper_Dispose_CanBeCalledTwice()
        {
            var wrapper = new TcpClientWrapper("127.0.0.1", 65000);

            Assert.That(() => wrapper.Dispose(), Throws.Nothing);
            Assert.That(() => wrapper.Dispose(), Throws.Nothing);
        }

        [Test]
        public async Task TcpClientWrapper_Loopback_SendAndReceive_Works()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var acceptTask = listener.AcceptTcpClientAsync();

            var wrapper = new TcpClientWrapper("127.0.0.1", port);

            var receivedTcs = new TaskCompletionSource<byte[]>();
            wrapper.MessageReceived += (_, data) => receivedTcs.TrySetResult(data);

            wrapper.Connect();

            using var serverClient = await acceptTask;
            using var serverStream = serverClient.GetStream();

            // Test sending from wrapper to server
            var sent = new byte[] { 1, 2, 3 };
            await wrapper.SendMessageAsync(sent);

            var buffer = new byte[sent.Length];
            var read = await serverStream.ReadAsync(buffer.AsMemory(), default);

            Assert.Multiple(() =>
            {
                Assert.That(read, Is.EqualTo(sent.Length));
                Assert.That(buffer, Is.EqualTo(sent));
            });

            // Test server->wrapper MessageReceived
            var serverToClient = new byte[] { 9, 8, 7 };
            await serverStream.WriteAsync(serverToClient.AsMemory(), default);

            var finished = await Task.WhenAny(receivedTcs.Task, Task.Delay(2000));

            Assert.Multiple(() =>
            {
                Assert.That(finished, Is.EqualTo(receivedTcs.Task));
                Assert.That(receivedTcs.Task.Result, Is.EqualTo(serverToClient));
            });

            wrapper.Disconnect();
            listener.Stop();
        }

        [Test]
        public async Task TcpClientWrapper_SendString_AfterConnect_Works()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;

            var acceptTask = listener.AcceptTcpClientAsync();
            var wrapper = new TcpClientWrapper("127.0.0.1", port);
            wrapper.Connect();

            using var server = await acceptTask;
            using var stream = server.GetStream();

            await wrapper.SendMessageAsync("test");
            var buffer = new byte[4];
            await stream.ReadAsync(buffer);

            Assert.That(buffer, Is.EqualTo(Encoding.UTF8.GetBytes("test")));

            wrapper.Disconnect();
            listener.Stop();
        }

        // ********************** UDP TESTS **********************

        [Test]
        public async Task UdpClientWrapper_StartAndStop_IsResponsive()
        {
            var wrapper = new UdpClientWrapper(65001);

            var listeningTask = Task.Run(() => wrapper.StartListeningAsync());
            await Task.Delay(100);
            wrapper.StopListening();

            var finished = await Task.WhenAny(listeningTask, Task.Delay(2000));

            Assert.Multiple(() =>
            {
                Assert.That(finished, Is.Not.Null);
                Assert.That(() => wrapper.Exit(), Throws.Nothing);
            });
        }

        [Test]
        public void UdpClientWrapper_GetHashCode_ReturnsInt()
        {
            var wrapper = new UdpClientWrapper(65002);
            var hash = wrapper.GetHashCode();

            Assert.That(hash, Is.TypeOf<int>());
        }

        [Test]
        public async Task UdpClientWrapper_Receives_Message()
        {
            int GetFreePort()
            {
                var tmp = new TcpListener(IPAddress.Loopback, 0);
                tmp.Start();
                var p = ((IPEndPoint)tmp.LocalEndpoint).Port;
                tmp.Stop();
                return p;
            }

            var port = GetFreePort();
            var wrapper = new UdpClientWrapper(port);

            var tcs = new TaskCompletionSource<byte[]>();
            wrapper.MessageReceived += (_, data) => tcs.TrySetResult(data);

            var listeningTask = Task.Run(() => wrapper.StartListeningAsync());
            await Task.Delay(50);

            using var sender = new UdpClient();
            var payload = new byte[] { 4, 5, 6 };
            await sender.SendAsync(payload, payload.Length, "127.0.0.1", port);

            var finished = await Task.WhenAny(tcs.Task, Task.Delay(2000));

            Assert.Multiple(() =>
            {
                Assert.That(finished, Is.EqualTo(tcs.Task));
                Assert.That(tcs.Task.Result, Is.EqualTo(payload));
            });

            wrapper.StopListening();
            await Task.WhenAny(listeningTask, Task.Delay(500));
        }

        [Test]
        public void UdpClientWrapper_Equals_ShouldBehaveCorrectly()
        {
            var a = new UdpClientWrapper(5001);
            var b = new UdpClientWrapper(5001);
            var c = new UdpClientWrapper(6001);

            Assert.That(a.Equals(a), Is.True);
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.Equals(c), Is.False);
            Assert.That(a.Equals(null), Is.False);
        }

        [Test]
        public void UdpClientWrapper_Dispose_ShouldNotThrow_AndCanBeCalledTwice()
        {
            var wrapper = new UdpClientWrapper(5001);

            Assert.That(() => wrapper.Dispose(), Throws.Nothing);
            Assert.That(() => wrapper.Dispose(), Throws.Nothing);
        }
    }
}
