using NetSdrClientApp.Messages;
using NetSdrClientApp.Networking;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static NetSdrClientApp.Messages.NetSdrMessageHelper;

namespace NetSdrClientApp
{
    public class NetSdrClient
    {
        private readonly ITcpClient _tcpClient;
        private readonly IUdpClient _udpClient;

        public bool IQStarted { get; private set; }

        public NetSdrClient(ITcpClient tcpClient, IUdpClient udpClient)
        {
            _tcpClient = tcpClient;
            _udpClient = udpClient;

            _tcpClient.MessageReceived += _tcpClient_MessageReceived;
            _udpClient.MessageReceived += _udpClient_MessageReceived;
        }

        public async Task ConnectAsync()
        {
            if (!_tcpClient.Connected)
            {
                _tcpClient.Connect();

                var sampleRate = BitConverter.GetBytes((long)100000).Take(5).ToArray();
                var automaticFilterMode = BitConverter.GetBytes((ushort)0).ToArray();
                var adMode = new byte[] { 0x00, 0x03 };

                var msgs = new List<byte[]>
                {
                    GetControlItemMessage(MsgTypes.SetControlItem, ControlItemCodes.IQOutputDataSampleRate, sampleRate),
                    GetControlItemMessage(MsgTypes.SetControlItem, ControlItemCodes.RFFilter, automaticFilterMode),
                    GetControlItemMessage(MsgTypes.SetControlItem, ControlItemCodes.ADModes, adMode),
                };

                foreach (var msg in msgs)
                {
                    await SendTcpRequest(msg);
                }
            }
        }

        public void Disconect()
        {
            _tcpClient.Disconnect();
            if (IQStarted)
            {
                _udpClient.StopListening();
                IQStarted = false;
            }
        }

        public async Task StartIQAsync()
        {
            if (!_tcpClient.Connected)
            {
                Console.WriteLine("No active connection.");
                return;
            }

            var msg = GetControlItemMessage(MsgTypes.SetControlItem, ControlItemCodes.ReceiverState,
                new byte[] { 0x80, 0x02, 0x01, 0x01 });

            await SendTcpRequest(msg);

            IQStarted = true;
            _ = _udpClient.StartListeningAsync();
        }

        public async Task StopIQAsync()
        {
            if (!_tcpClient.Connected)
            {
                Console.WriteLine("No active connection.");
                return;
            }

            var msg = GetControlItemMessage(
                MsgTypes.SetControlItem,
                ControlItemCodes.ReceiverState,
                new byte[] { 0x00, 0x01, 0x00, 0x00 });

            await SendTcpRequest(msg);

            IQStarted = false;
            _udpClient.StopListening();
        }

        public async Task ChangeFrequencyAsync(long hz, int channel)
        {
            if (!_tcpClient.Connected)
            {
                Console.WriteLine("No active connection.");
                return;
            }

            var args = new[] { (byte)channel }
                .Concat(BitConverter.GetBytes(hz).Take(5))
                .ToArray();

            var msg = GetControlItemMessage(
                MsgTypes.SetControlItem,
                ControlItemCodes.ReceiverFrequency,
                args);

            await SendTcpRequest(msg);
        }

        // ─────────────────────────────────────────────── UDP ───────────────────────────────────────────────

        private static void _udpClient_MessageReceived(object? sender, byte[] e)
        {
            _ = TranslateMessage(e, out _, out _, out _, out byte[] body);
            var samples = GetSamples(16, body);

            Console.WriteLine($"Samples received: {BitConverter.ToString(body)}");

            using var fs = new FileStream("samples.bin", FileMode.Append, FileAccess.Write, FileShare.Read);
            using var bw = new BinaryWriter(fs);
            foreach (var sample in samples)
            {
                bw.Write((short)sample);
            }
        }

        // ─────────────────────────────────────────────── TCP ───────────────────────────────────────────────

        private TaskCompletionSource<byte[]>? responseTaskSource;

        private async Task<byte[]> SendTcpRequest(byte[] msg)
        {
            if (!_tcpClient.Connected)
            {
                Console.WriteLine("No active connection.");
                return Array.Empty<byte>();
            }

            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            responseTaskSource = tcs;

            await _tcpClient.SendMessageAsync(msg);

            return await tcs.Task;
        }

        private void _tcpClient_MessageReceived(object? sender, byte[] e)
        {
            var pending = responseTaskSource;

            if (pending != null)
            {
                pending.TrySetResult(e);
                responseTaskSource = null!;
            }
            else
            {
                Console.WriteLine("Received unsolicited message.");
            }

            Console.WriteLine($"Response received: {BitConverter.ToString(e)}");
        }
    }
}
