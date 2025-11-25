using System;
using System.Linq;
using NUnit.Framework;
using NetSdrClientApp.Messages;

namespace NetSdrClientAppTests
{
    public class NetSdrMessageHelperTests
    {
        [SetUp]
        public void Setup() { }

        [Test]
        public void GetControlItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.Ack;
            var code = NetSdrMessageHelper.ControlItemCodes.ReceiverState;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(type, code, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var codeBytes = msg.Skip(2).Take(2);
            var parametersBytes = msg.Skip(4);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);
            var actualCode = BitConverter.ToInt16(codeBytes.ToArray());

            //Assert
            Assert.Multiple(() =>
            {
                Assert.That(headerBytes.ToArray(), Has.Length.EqualTo(2));
                Assert.That(msg, Has.Length.EqualTo(actualLength));
                Assert.That(type, Is.EqualTo(actualType));
                Assert.That(actualCode, Is.EqualTo((short)code));
                Assert.That(parametersBytes.ToArray(), Has.Length.EqualTo(parametersLength));
            });
        }

        [Test]
        public void GetDataItemMessageTest()
        {
            //Arrange
            var type = NetSdrMessageHelper.MsgTypes.DataItem2;
            int parametersLength = 7500;

            //Act
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(type, new byte[parametersLength]);

            var headerBytes = msg.Take(2);
            var parametersBytes = msg.Skip(2);

            var num = BitConverter.ToUInt16(headerBytes.ToArray());
            var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
            var actualLength = num - ((int)actualType << 13);

            //Assert
            Assert.Multiple(() =>
            {
                Assert.That(headerBytes.ToArray(), Has.Length.EqualTo(2));
                Assert.That(msg, Has.Length.EqualTo(actualLength));
                Assert.That(type, Is.EqualTo(actualType));
                Assert.That(parametersBytes.ToArray(), Has.Length.EqualTo(parametersLength));
            });
        }

        // ------------------- Tests for GetSamples -------------------

        [Test]
        public void GetSamples_ReturnsCorrectValues_For8bitSamples()
        {
            byte[] body = { 1, 2, 3, 4 };
            ushort sampleSize = 8;

            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(samples, Has.Length.EqualTo(4));
                Assert.That(samples[0], Is.EqualTo(1));
                Assert.That(samples[1], Is.EqualTo(2));
                Assert.That(samples[2], Is.EqualTo(3));
                Assert.That(samples[3], Is.EqualTo(4));
            });
        }

        [Test]
        public void GetSamples_ReturnsCorrectValues_For16bitSamples()
        {
            byte[] body = { 1, 0, 2, 0 };
            ushort sampleSize = 16;

            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(samples, Has.Length.EqualTo(2));
                Assert.That(samples[0], Is.EqualTo(1));
                Assert.That(samples[1], Is.EqualTo(2));
            });
        }

        [Test]
        public void GetSamples_ReturnsCorrectValues_For24bitSamples()
        {
            byte[] body = { 1, 2, 3, 4, 5, 6 };
            ushort sampleSize = 24;

            var samples = NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(samples, Has.Length.EqualTo(2));
                Assert.That(samples[0], Is.EqualTo(BitConverter.ToInt32(new byte[] { 1, 2, 3, 0 }, 0)));
                Assert.That(samples[1], Is.EqualTo(BitConverter.ToInt32(new byte[] { 4, 5, 6, 0 }, 0)));
            });
        }

        [Test]
        public void GetSamples_ThrowsArgumentOutOfRangeException_WhenSampleSizeTooBig()
        {
            byte[] body = { 1, 2, 3, 4 };
            ushort sampleSize = 40;

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                NetSdrMessageHelper.GetSamples(sampleSize, body).ToArray();
            });
        }

        // ------------------- Extra coverage tests -------------------

        [Test]
        public void TranslateMessage_ControlItemMessage_Success()
        {
            byte[] msg = NetSdrMessageHelper.GetControlItemMessage(
                NetSdrMessageHelper.MsgTypes.SetControlItem,
                NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency,
                new byte[] { 0xFF, 0xEE }
            );

            bool result = NetSdrMessageHelper.TranslateMessage(msg,
                out var type, out var code, out var seqNum, out var body);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(type, Is.EqualTo(NetSdrMessageHelper.MsgTypes.SetControlItem));
                Assert.That(code, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency));
                Assert.That(body, Is.EqualTo(new byte[] { 0xFF, 0xEE }));
            });
        }

        [Test]
        public void TranslateMessage_DataItemMessage_Success()
        {
            byte[] msg = NetSdrMessageHelper.GetDataItemMessage(
                NetSdrMessageHelper.MsgTypes.DataItem0,
                new byte[] { 0x11, 0x22 }
            );

            bool result = NetSdrMessageHelper.TranslateMessage(msg,
                out var type, out var code, out var seqNum, out var body);

            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(type, Is.EqualTo(NetSdrMessageHelper.MsgTypes.DataItem0));
                Assert.That(code, Is.EqualTo(NetSdrMessageHelper.ControlItemCodes.None));
                Assert.That(seqNum, Is.Not.EqualTo(0));
            });
        }

        [Test]
        public void GetSamples_EmptyBody_ReturnsEmpty()
        {
            var samples = NetSdrMessageHelper.GetSamples(8, Array.Empty<byte>()).ToArray();

            Assert.That(samples, Has.Length.EqualTo(0));
        }

        [Test]
        public void GetSamples_For32bitSamples()
        {
            byte[] body = { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08 };

            var samples = NetSdrMessageHelper.GetSamples(32, body).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(samples, Has.Length.EqualTo(2));
                Assert.That(samples[0], Is.EqualTo(BitConverter.ToInt32(new byte[] { 0x01, 0x02, 0x03, 0x04 }, 0)));
                Assert.That(samples[1], Is.EqualTo(BitConverter.ToInt32(new byte[] { 0x05, 0x06, 0x07, 0x08 }, 0)));
            });
        }

        [Test]
        public void TranslateMessage_InvalidItemCode()
        {
            byte[] msg =
            {
                0x04, 0x00, // Header
                0xFF, 0xFF  // Invalid item code
            };

            bool result = NetSdrMessageHelper.TranslateMessage(msg,
                out var type, out var code, out var seqNum, out var body);

            Assert.That(result, Is.False);
        }

        [Test]
        public void GetControlItemMessage_AllMsgTypes()
        {
            var controlTypes = new[]
            {
                NetSdrMessageHelper.MsgTypes.SetControlItem,
                NetSdrMessageHelper.MsgTypes.CurrentControlItem,
                NetSdrMessageHelper.MsgTypes.ControlItemRange,
                NetSdrMessageHelper.MsgTypes.Ack
            };

            foreach (var type in controlTypes)
            {
                byte[] msg = NetSdrMessageHelper.GetControlItemMessage(
                    type,
                    NetSdrMessageHelper.ControlItemCodes.ReceiverFrequency,
                    new byte[] { 0x01, 0x02 }
                );

                Assert.Multiple(() =>
                {
                    Assert.That(msg, Has.Length.GreaterThan(0));
                    var num = BitConverter.ToUInt16(msg.Take(2).ToArray());
                    var actualType = (NetSdrMessageHelper.MsgTypes)(num >> 13);
                    Assert.That(actualType, Is.EqualTo(type));
                });
            }
        }
        
        
        [Test]
        public void TranslateMessage_Fails_WhenBodyLengthDoesNotMatchHeaderLength()
        {
            // Header says length = 4, but total msg length is 3 → invalid!
            byte[] msg = { 0x04, 0x04, 0x00 };

            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                NetSdrMessageHelper.TranslateMessage(msg,
                    out _, out _, out _, out _);
            });
        }


        [Test]
        public void GetSamples_Throws_WhenSampleSizeNotDivisibleBy8()
        {
            byte[] body = { 1, 2, 3, 4 };
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NetSdrMessageHelper.GetSamples(12, body).ToArray()); // 12 bits is invalid here
        }

        [Test]
        public void GetSamples_Throws_WhenSampleSizeLessThan8()
        {
            byte[] body = { 1, 2, 3, 4 };
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                NetSdrMessageHelper.GetSamples(0, body).ToArray());
        }
    }
}
