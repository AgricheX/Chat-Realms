using System;
using System.IO;
using System.IO.Compression;

namespace ChatRealms
{
    internal static class Png
    {
        private static readonly uint[] CrcTable = BuildCrc();

        private static uint[] BuildCrc()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        private static uint Crc(uint crc, byte[] buf, int off, int len)
        {
            uint c = crc;
            for (int i = off; i < off + len; i++) c = CrcTable[(c ^ buf[i]) & 0xFF] ^ (c >> 8);
            return c;
        }

        private static void Adler(byte[] buf, int len, ref uint a, ref uint b)
        {
            int i = 0;
            while (len > 0)
            {
                int n = len < 5552 ? len : 5552;
                len -= n;
                while (n-- > 0)
                {
                    a += buf[i++];
                    b += a;
                }
                a %= 65521;
                b %= 65521;
            }
        }

        private static int Cost(byte v)
        {
            return v < 128 ? v : 256 - v;
        }

        private static void WriteInt(Stream s, int v)
        {
            s.WriteByte((byte)((v >> 24) & 0xFF));
            s.WriteByte((byte)((v >> 16) & 0xFF));
            s.WriteByte((byte)((v >> 8) & 0xFF));
            s.WriteByte((byte)(v & 0xFF));
        }

        private static void Chunk(Stream s, string type, byte[] data, int len)
        {
            WriteInt(s, len);
            byte[] t = { (byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3] };
            s.Write(t, 0, 4);
            if (len > 0) s.Write(data, 0, len);
            uint crc = Crc(0xFFFFFFFFu, t, 0, 4);
            if (len > 0) crc = Crc(crc, data, 0, len);
            WriteInt(s, (int)(crc ^ 0xFFFFFFFFu));
        }

        public static byte[] Encode(byte[] rgb, int width, int height, int stride, int scale)
        {
            if (scale < 1) scale = 1;
            int ow = width * scale;
            int oh = height * scale;
            int rowLen = ow * 3;
            byte[] cur = new byte[rowLen];
            byte[] prev = new byte[rowLen];
            byte[] sub = new byte[rowLen];
            byte[] up = new byte[rowLen];
            byte[] head = new byte[1];
            uint a = 1;
            uint b = 0;
            var idat = new MemoryStream(Math.Max(4096, rowLen * oh / 6));
            idat.WriteByte(0x78);
            idat.WriteByte(0x9C);
            using (var z = new DeflateStream(idat, CompressionMode.Compress, true))
            {
                for (int y = 0; y < oh; y++)
                {
                    int src = (height - 1 - y / scale) * stride;
                    if (scale == 1)
                    {
                        Buffer.BlockCopy(rgb, src, cur, 0, rowLen);
                    }
                    else
                    {
                        int o = 0;
                        for (int x = 0; x < width; x++)
                        {
                            int p = src + x * 3;
                            byte r = rgb[p];
                            byte g = rgb[p + 1];
                            byte bl = rgb[p + 2];
                            for (int k = 0; k < scale; k++)
                            {
                                cur[o++] = r;
                                cur[o++] = g;
                                cur[o++] = bl;
                            }
                        }
                    }
                    long none = 0;
                    long costSub = 0;
                    long costUp = 0;
                    for (int i = 0; i < rowLen; i++)
                    {
                        byte left = i >= 3 ? cur[i - 3] : (byte)0;
                        byte vs = (byte)(cur[i] - left);
                        byte vu = (byte)(cur[i] - prev[i]);
                        sub[i] = vs;
                        up[i] = vu;
                        none += Cost(cur[i]);
                        costSub += Cost(vs);
                        costUp += Cost(vu);
                    }
                    byte type;
                    byte[] line;
                    if (costUp <= costSub && costUp <= none)
                    {
                        type = 2;
                        line = up;
                    }
                    else if (costSub <= none)
                    {
                        type = 1;
                        line = sub;
                    }
                    else
                    {
                        type = 0;
                        line = cur;
                    }
                    head[0] = type;
                    z.Write(head, 0, 1);
                    z.Write(line, 0, rowLen);
                    Adler(head, 1, ref a, ref b);
                    Adler(line, rowLen, ref a, ref b);
                    byte[] swap = prev;
                    prev = cur;
                    cur = swap;
                }
            }
            uint adler = (b << 16) | a;
            WriteInt(idat, (int)adler);

            var png = new MemoryStream((int)idat.Length + 128);
            byte[] sig = { 137, 80, 78, 71, 13, 10, 26, 10 };
            png.Write(sig, 0, sig.Length);
            byte[] ihdr = new byte[13];
            ihdr[0] = (byte)((ow >> 24) & 0xFF);
            ihdr[1] = (byte)((ow >> 16) & 0xFF);
            ihdr[2] = (byte)((ow >> 8) & 0xFF);
            ihdr[3] = (byte)(ow & 0xFF);
            ihdr[4] = (byte)((oh >> 24) & 0xFF);
            ihdr[5] = (byte)((oh >> 16) & 0xFF);
            ihdr[6] = (byte)((oh >> 8) & 0xFF);
            ihdr[7] = (byte)(oh & 0xFF);
            ihdr[8] = 8;
            ihdr[9] = 2;
            Chunk(png, "IHDR", ihdr, ihdr.Length);
            Chunk(png, "IDAT", idat.GetBuffer(), (int)idat.Length);
            Chunk(png, "IEND", new byte[0], 0);
            return png.ToArray();
        }
    }
}
