using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace ChatRealms
{
    internal class WebSocketClient : IDisposable
    {
        private TcpClient _tcp;
        private Stream _stream;
        private readonly object _sendLock = new object();
        private readonly Random _rng = new Random();
        private volatile bool _connected;

        public bool Connected { get { return _connected; } }

        public void Connect(string url, bool insecureTls, int timeoutMs)
        {
            Uri uri = new Uri(url);
            bool tls = uri.Scheme == "wss";
            int port = uri.Port > 0 ? uri.Port : (tls ? 443 : 80);
            _tcp = new TcpClient();
            var ar = _tcp.BeginConnect(uri.Host, port, null, null);
            if (!ar.AsyncWaitHandle.WaitOne(timeoutMs)) throw new IOException("connect timeout");
            _tcp.EndConnect(ar);
            _tcp.NoDelay = true;
            _tcp.ReceiveTimeout = 90000;
            Stream s = _tcp.GetStream();
            if (tls)
            {
                RemoteCertificateValidationCallback cb = null;
                if (insecureTls) cb = (a, b, c, d) => true;
                var ssl = new SslStream(s, false, cb);
                ssl.AuthenticateAsClient(uri.Host);
                s = ssl;
            }
            _stream = s;

            byte[] keyBytes = new byte[16];
            lock (_rng) _rng.NextBytes(keyBytes);
            string key = Convert.ToBase64String(keyBytes);
            string path = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;
            string req = "GET " + path + " HTTP/1.1\r\n" +
                         "Host: " + uri.Host + (uri.IsDefaultPort ? "" : ":" + uri.Port) + "\r\n" +
                         "Upgrade: websocket\r\nConnection: Upgrade\r\n" +
                         "Sec-WebSocket-Key: " + key + "\r\nSec-WebSocket-Version: 13\r\n\r\n";
            byte[] reqBytes = Encoding.ASCII.GetBytes(req);
            _stream.Write(reqBytes, 0, reqBytes.Length);
            _stream.Flush();

            var sb = new StringBuilder();
            while (!sb.ToString().EndsWith("\r\n\r\n", StringComparison.Ordinal))
            {
                int b = _stream.ReadByte();
                if (b < 0) throw new IOException("handshake closed");
                sb.Append((char)b);
                if (sb.Length > 16384) throw new IOException("handshake too large");
            }
            string head = sb.ToString();
            if (!head.StartsWith("HTTP/1.1 101", StringComparison.Ordinal)) throw new IOException("handshake refused");
            string expected;
            using (var sha = SHA1.Create())
            {
                expected = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
            }
            if (head.IndexOf(expected, StringComparison.Ordinal) < 0) throw new IOException("bad accept key");
            _connected = true;
        }

        public void SendText(string text)
        {
            Send(1, Encoding.UTF8.GetBytes(text));
        }

        private void Send(int opcode, byte[] payload)
        {
            int len = payload.Length;
            int head = len < 126 ? 2 : (len < 65536 ? 4 : 10);
            byte[] frame = new byte[head + 4 + len];
            frame[0] = (byte)(0x80 | opcode);
            if (len < 126)
            {
                frame[1] = (byte)(0x80 | len);
            }
            else if (len < 65536)
            {
                frame[1] = 0x80 | 126;
                frame[2] = (byte)(len >> 8);
                frame[3] = (byte)(len & 0xFF);
            }
            else
            {
                frame[1] = 0x80 | 127;
                for (int i = 0; i < 8; i++) frame[2 + i] = (byte)(((long)len >> (8 * (7 - i))) & 0xFF);
            }
            byte[] mask = new byte[4];
            lock (_rng) _rng.NextBytes(mask);
            Buffer.BlockCopy(mask, 0, frame, head, 4);
            int off = head + 4;
            for (int i = 0; i < len; i++) frame[off + i] = (byte)(payload[i] ^ mask[i & 3]);
            lock (_sendLock)
            {
                Stream s = _stream;
                if (s == null) throw new IOException("not connected");
                s.Write(frame, 0, frame.Length);
                s.Flush();
            }
        }

        private bool ReadExact(byte[] buf, int count)
        {
            Stream s = _stream;
            if (s == null) return false;
            int off = 0;
            while (off < count)
            {
                int n = s.Read(buf, off, count - off);
                if (n <= 0) return false;
                off += n;
            }
            return true;
        }

        public string ReceiveText()
        {
            var message = new MemoryStream();
            int msgOpcode = 0;
            while (true)
            {
                byte[] h = new byte[2];
                if (!ReadExact(h, 2)) return null;
                bool fin = (h[0] & 0x80) != 0;
                int opcode = h[0] & 0x0F;
                bool masked = (h[1] & 0x80) != 0;
                long len = h[1] & 0x7F;
                if (len == 126)
                {
                    byte[] e = new byte[2];
                    if (!ReadExact(e, 2)) return null;
                    len = (e[0] << 8) | e[1];
                }
                else if (len == 127)
                {
                    byte[] e = new byte[8];
                    if (!ReadExact(e, 8)) return null;
                    len = 0;
                    for (int i = 0; i < 8; i++) len = (len << 8) | e[i];
                }
                if (len < 0 || len > 16 * 1024 * 1024) return null;
                byte[] mask = null;
                if (masked)
                {
                    mask = new byte[4];
                    if (!ReadExact(mask, 4)) return null;
                }
                byte[] payload = new byte[len];
                if (len > 0 && !ReadExact(payload, (int)len)) return null;
                if (masked)
                {
                    for (int i = 0; i < payload.Length; i++) payload[i] ^= mask[i % 4];
                }
                if (opcode == 8) return null;
                if (opcode == 9)
                {
                    Send(10, payload);
                    continue;
                }
                if (opcode == 10) continue;
                if (opcode == 1 || opcode == 2) msgOpcode = opcode;
                message.Write(payload, 0, payload.Length);
                if (fin)
                {
                    if (msgOpcode == 1) return Encoding.UTF8.GetString(message.ToArray());
                    message.SetLength(0);
                }
            }
        }

        public void Dispose()
        {
            _connected = false;
            try
            {
                if (_stream != null) _stream.Close();
            }
            catch (Exception)
            {
            }
            try
            {
                if (_tcp != null) _tcp.Close();
            }
            catch (Exception)
            {
            }
            _stream = null;
            _tcp = null;
        }
    }
}
