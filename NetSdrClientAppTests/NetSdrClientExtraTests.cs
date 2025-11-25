using Moq;
using NetSdrClientApp;
using NetSdrClientApp.Networking;
using NetSdrClientApp.Messages;
using NUnit.Framework;
using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace NetSdrClientAppTests
{
    public class NetSdrClientExtraTests
    {
        [SetUp]
        public void Setup() { }

        [Test]
        public async Task SendTcpRequest_ReturnsEmpty_WhenNotConnected()
        {
            var tcpMock = new Mock<ITcpClient>();
            var udpMock = new Mock<IUdpClient>();
            tcpMock.SetupGet(t => t.Connected).Returns(false);

            var client = new NetSdrClient(tcpMock.Object, udpMock.Object);

            var method = typeof(NetSdrClient).GetMethod("SendTcpRequest", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var task = (Task<byte[]>)method.Invoke(client, new object[] { new byte[] { 0x01 } })!;
            var result = await task;

            Assert.That(result, Is.Empty);
        }

        [Test]
        public async Task SendTcpRequest_ReturnsResponse_WhenConnected()
        {
            var tcpMock = new Mock<ITcpClient>();
            var udpMock = new Mock<IUdpClient>();
            tcpMock.SetupGet(t => t.Connected).Returns(true);

            // When SendMessageAsync is called, raise MessageReceived immediately with sample response
            tcpMock.Setup(t => t.SendMessageAsync(It.IsAny<byte[]>())).Returns(Task.CompletedTask).Callback(() =>
            {
                var response = new byte[] { 0xAA, 0xBB };
                tcpMock.Raise(t => t.MessageReceived += null, tcpMock.Object, response);
            });

            var client = new NetSdrClient(tcpMock.Object, udpMock.Object);

            var method = typeof(NetSdrClient).GetMethod("SendTcpRequest", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var task = (Task<byte[]>)method.Invoke(client, new object[] { new byte[] { 0x02 } })!;
            var result = await task;

            Assert.That(result, Is.EqualTo(new byte[] { 0xAA, 0xBB }));
        }

        [Test]
        public void UdpClientMessageReceived_WritesSamplesFile()
        {
            var body = new byte[] { 0x01, 0x00, 0x02, 0x01, 0x04, 0x03 };
            var msg = NetSdrMessageHelper.GetDataItemMessage(NetSdrMessageHelper.MsgTypes.DataItem0, body);

            var samplesFile = Path.Combine(Directory.GetCurrentDirectory(), "samples.bin");
            if (File.Exists(samplesFile)) File.Delete(samplesFile);

            var method = typeof(NetSdrClient).GetMethod("_udpClient_MessageReceived", BindingFlags.NonPublic | BindingFlags.Static)!;
            method.Invoke(null, new object?[] { null, msg });

            Assert.That(File.Exists(samplesFile), Is.True);

            var data = File.ReadAllBytes(samplesFile);

            // Two samples written as 16-bit shorts => 4 bytes
            Assert.That(data, Has.Length.GreaterThanOrEqualTo(4));

            File.Delete(samplesFile);
        }
    }
}
