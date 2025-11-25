using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using EchoTspServer;
using NUnit.Framework;

namespace EchoServerTests
{
    [TestFixture]
    public class UdpTimedSenderTests
    {
        [Test, Timeout(2000)]
        public async Task StartSending_SendsUdpPackets()
        {
            using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

            var ep = receiver.Client.LocalEndPoint as IPEndPoint;
            Assert.That(ep, Is.Not.Null);

            int port = ep!.Port;

            using var sender = new UdpTimedSender("127.0.0.1", port);
            sender.StartSending(50);

            Task<UdpReceiveResult> recvTask = receiver.ReceiveAsync();
            Task completed = await Task.WhenAny(recvTask, Task.Delay(1000));

            sender.StopSending();

            Assert.That(completed, Is.EqualTo(recvTask), "No UDP packet received within timeout.");
        }

        [Test]
        public void StartSending_CalledTwice_Throws()
        {
            using var sender = new UdpTimedSender("127.0.0.1", 5001);
            sender.StartSending(30);

            Assert.That(() => sender.StartSending(30), Throws.InvalidOperationException);
        }

        [Test]
        public void StopSending_CanBeCalledMultipleTimes()
        {
            using var sender = new UdpTimedSender("127.0.0.1", 5001);

            sender.StartSending(30);

            Assert.That(() => sender.StopSending(), Throws.Nothing);
            Assert.That(() => sender.StopSending(), Throws.Nothing);
        }

        [Test]
        public void Dispose_ReleasesResources_AndCanBeCalledTwice()
        {
            var sender = new UdpTimedSender("127.0.0.1", 5001);

            Assert.That(() => sender.Dispose(), Throws.Nothing);
            Assert.That(() => sender.Dispose(), Throws.Nothing);
        }

        [Test, Timeout(2500)]
        public async Task SentPacket_Has_CorrectHeader_AndCounter()
        {
            using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));

            var ep = receiver.Client.LocalEndPoint as IPEndPoint;
            Assert.That(ep, Is.Not.Null);

            int port = ep!.Port;

            using var sender = new UdpTimedSender("127.0.0.1", port);
            sender.StartSending(40);

            UdpReceiveResult res = await receiver.ReceiveAsync();
            sender.StopSending();

            byte[] data = res.Buffer;

            Assert.Multiple(() =>
            {
                Assert.That(data.Length, Is.GreaterThan(6), "Packet too small.");

                Assert.That(data[0], Is.EqualTo(0x04));
                Assert.That(data[1], Is.EqualTo(0x84));

                ushort counter = BitConverter.ToUInt16(data, 2);
                Assert.That(counter, Is.GreaterThan(0));
            });
        }
    }
}
