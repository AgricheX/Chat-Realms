using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace ChatRealms
{
    internal struct ChatMessage
    {
        public string User;
        public string Text;
    }

    internal class TwitchChat
    {
        private volatile int _gen;
        private volatile bool _run;
        private volatile TcpClient _client;
        private volatile string _channel = "";
        private readonly Random _rng = new Random();

        public readonly ConcurrentQueue<ChatMessage> Inbox = new ConcurrentQueue<ChatMessage>();
        public volatile string Status = "off";
        public string Channel { get { return _channel; } }
        public bool Running { get { return _run; } }

        private bool Alive(int gen)
        {
            return _run && gen == _gen;
        }

        public void Start(string channel)
        {
            Stop();
            string ch = (channel ?? "").Trim().TrimStart('#').ToLowerInvariant();
            _channel = ch;
            if (ch.Length == 0)
            {
                Status = "no channel";
                return;
            }
            _run = true;
            int gen = _gen;
            var t = new Thread(() => Loop(gen, ch));
            t.IsBackground = true;
            t.Start();
        }

        public void Stop()
        {
            _gen++;
            _run = false;
            var c = _client;
            _client = null;
            try
            {
                if (c != null) c.Close();
            }
            catch (Exception)
            {
            }
            ChatMessage drop;
            while (Inbox.TryDequeue(out drop))
            {
            }
            Status = "off";
        }

        private void Loop(int gen, string channel)
        {
            int backoff = 2000;
            while (Alive(gen))
            {
                var c = new TcpClient();
                try
                {
                    _client = c;
                    if (Alive(gen))
                    {
                        Status = "connecting";
                        c.ReceiveTimeout = 420000;
                        c.Connect("irc.chat.twitch.tv", 6667);
                        var stream = c.GetStream();
                        var reader = new StreamReader(stream, new UTF8Encoding(false));
                        var writer = new StreamWriter(stream, new UTF8Encoding(false));
                        writer.NewLine = "\r\n";
                        writer.AutoFlush = true;
                        writer.WriteLine("PASS SCHMOOPIIE");
                        int nick;
                        lock (_rng) nick = _rng.Next(10000, 99999);
                        writer.WriteLine("NICK justinfan" + nick);
                        writer.WriteLine("JOIN #" + channel);
                        if (Alive(gen)) Status = "connected";
                        backoff = 2000;
                        string line;
                        while (Alive(gen) && (line = reader.ReadLine()) != null)
                        {
                            if (line.StartsWith("PING", StringComparison.Ordinal))
                            {
                                writer.WriteLine("PONG :tmi.twitch.tv");
                                continue;
                            }
                            if (Alive(gen)) Parse(line);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (Alive(gen)) Status = "error: " + e.Message;
                }
                try
                {
                    c.Close();
                }
                catch (Exception)
                {
                }
                if (_client == c) _client = null;
                if (!Alive(gen)) break;
                for (int waited = 0; waited < backoff && Alive(gen); waited += 250) Thread.Sleep(250);
                backoff = Math.Min(backoff * 2, 30000);
            }
        }

        private void Parse(string line)
        {
            int idx = line.IndexOf(" PRIVMSG ", StringComparison.Ordinal);
            if (idx < 0 || line.Length < 2 || line[0] != ':') return;
            int bang = line.IndexOf('!');
            if (bang < 1 || bang > idx) return;
            string user = line.Substring(1, bang - 1);
            int msgStart = line.IndexOf(" :", idx, StringComparison.Ordinal);
            if (msgStart < 0) return;
            string text = line.Substring(msgStart + 2).Trim();
            if (text.Length == 0) return;
            if (Inbox.Count > 5000) return;
            Inbox.Enqueue(new ChatMessage { User = user.ToLowerInvariant(), Text = text });
        }
    }
}
