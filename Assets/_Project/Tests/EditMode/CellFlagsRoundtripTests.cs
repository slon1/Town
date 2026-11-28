using NUnit.Framework;
using ZombieCity.City.Core;

namespace ZombieCity.City.Core.Tests
{
    /// <summary>
    /// Bit 15 is the sign bit of a signed short. Flags stay ushort and little-endian bytes (TASK-014).
    /// </summary>
    public class CellFlagsRoundtripTests
    {
        [Test]
        public void AllSixteenBits_RoundTrip_LittleEndianBytes_AndClassifyAll()
        {
            var flags = new ushort[16];
            for (int b = 0; b < 16; b++)
            {
                flags[b] = (ushort)(1 << b);
                Assert.Greater(flags[b], 0, "bit " + b + " stored in ushort must stay positive");
            }

            Assert.AreEqual((ushort)32768, (ushort)CellFlags.Tram);
            Assert.AreEqual(flags[15], (ushort)CellFlags.Tram);

            var bytes = new byte[32];
            for (int i = 0; i < 16; i++)
            {
                bytes[i * 2] = (byte)flags[i];
                bytes[i * 2 + 1] = (byte)(flags[i] >> 8);
            }

            var back = new ushort[16];
            for (int i = 0; i < 16; i++)
            {
                back[i] = (ushort)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
            }

            var access = new byte[16];
            AccessClassifier.ClassifyAll(back, access);

            for (int b = 0; b < 16; b++)
            {
                Assert.AreEqual(flags[b], back[b], "bit " + b);
                CellFlags bit = (CellFlags)back[b];
                CityAccess expected;
                if (b == 3 || b == 4 || b == 5 || b == 6)
                {
                    expected = CityAccess.Obstacle;
                }
                else if (b == 0)
                {
                    expected = CityAccess.Roadway;
                }
                else
                {
                    expected = CityAccess.Pedestrian;
                }
                Assert.AreEqual(expected, AccessClassifier.Classify(bit), "Classify bit " + b);
                Assert.AreEqual((byte)expected, access[b], "ClassifyAll bit " + b);
            }
        }
    }
}
