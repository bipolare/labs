using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using System;
using System.IO;
using NUnit.Framework;

namespace NetSdrClientAppTests
{
    public class NetSdrClientTests
    {
        NetSdrClient _client;
        Mock<ITcpClient> _tcpMock;
        Mock<IUdpClient> _updMock;

        [SetUp]
        public void Setup()
        {
            _tcpMock = new Mock<ITcpClient>();
            _tcpMock.Setup(tcp => tcp.Connect()).Callback(() =>
            {
                _tcpMock.Setup(tcp => tcp.Connected).Returns(true);
            });

            _tcpMock.Setup(tcp => tcp.Disconnect()).Callback(() =>
            {
                _tcpMock.Setup(tcp => tcp.Connected).Returns(false);
            });

            _tcpMock.Setup(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>())).Callback<byte[]>((bytes) =>
            {
                // simulate that server responds by raising MessageReceived
                _tcpMock.Raise(tcp => tcp.MessageReceived += null, _tcpMock.Object, bytes);
            });

            _updMock = new Mock<IUdpClient>();

            _client = new NetSdrClient(_tcpMock.Object, _updMock.Object);
        }

        [Test]
        public async System.Threading.Tasks.Task ConnectAsync_SendsInitMessages()
        {
            await _client.ConnectAsync();

            _tcpMock.Verify(tcp => tcp.Connect(), Times.Once);
            _tcpMock.Verify(tcp => tcp.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeastOnce);
        }

        [Test]
        public void Disconnect_CallsDisconnect()
        {
            _client.Disconect();
            _tcpMock.Verify(tcp => tcp.Disconnect(), Times.Once);
        }

        [Test]
        public async System.Threading.Tasks.Task StartIQ_DoesNothingWhenNotConnected()
        {
            _tcpMock.Setup(t => t.Connected).Returns(false);
            await _client.StartIQAsync();
            _updMock.Verify(u => u.StartListeningAsync(), Times.Never);
        }

        [Test]
        public async System.Threading.Tasks.Task StartIQ_StartsWhenConnected()
        {
            await ConnectAsync_SendsInitMessages();
            await _client.StartIQAsync();
            _updMock.Verify(u => u.StartListeningAsync(), Times.Once);
            Assert.That(_client.IQStarted, Is.True);
        }

        [Test]
        public async System.Threading.Tasks.Task StopIQ_StopsListening()
        {
            await ConnectAsync_SendsInitMessages();
            await _client.StartIQAsync();

            await _client.StopIQAsync();

            _updMock.Verify(u => u.StopListening(), Times.Once);
            Assert.That(_client.IQStarted, Is.False);
        }

        [Test]
        public async System.Threading.Tasks.Task ChangeFrequency_SendsWhenConnected()
        {
            _tcpMock.Setup(t => t.Connected).Returns(true);
            await _client.ConnectAsync();
            await _client.ChangeFrequencyAsync(7000000, 1);
            _tcpMock.Verify(t => t.SendMessageAsync(It.IsAny<byte[]>()), Times.AtLeastOnce);
        }

        [Test]
        public async System.Threading.Tasks.Task ChangeFrequency_DoesNotSendWhenNotConnected()
        {
            _tcpMock.Setup(t => t.Connected).Returns(false);
            await _client.ChangeFrequencyAsync(7000000, 1);
            _tcpMock.Verify(t => t.SendMessageAsync(It.IsAny<byte[]>()), Times.Never);
        }

        [Test]
        public async System.Threading.Tasks.Task StopIQ_NoActiveConnection_PrintsMessage()
        {
            _tcpMock.Setup(t => t.Connected).Returns(false);
            var sw = new StringWriter();
            var original = Console.Out;
            Console.SetOut(sw);
            try
            {
                await _client.StopIQAsync();
                Assert.That(sw.ToString(), Does.Contain("No active connection."));
            }
            finally
            {
                Console.SetOut(original);
            }
        }
    }
}
