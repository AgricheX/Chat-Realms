using System;
using System.Collections.Concurrent;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ChatRealms
{
    internal class RelayRequest
    {
        public string Id;
        public string UserId;
        public string UserName;
        public string Kingdom;
        public string WorldId;
        public long UnitId;
        public string Action;
        public JObject Args;
        public JObject Lineage;
    }

    internal class RelayClient
    {
        private volatile int _gen;
        private volatile bool _run;
        private volatile WebSocketClient _ws;
        private string _url = "";
        private string _code = "";
        private bool _insecure;
        private readonly ConcurrentQueue<string> _out = new ConcurrentQueue<string>();
        private readonly AutoResetEvent _outReady = new AutoResetEvent(false);

        public readonly ConcurrentQueue<RelayRequest> Requests = new ConcurrentQueue<RelayRequest>();
        public volatile bool SnapshotWanted;
        public volatile string Status = "off";
        public volatile string Guild = "";
        public bool Connected { get { var w = _ws; return w != null && w.Connected && Guild.Length > 0; } }

        public bool SameConfig(string url, string code, bool insecure)
        {
            return _run && _url == url && _code == code && _insecure == insecure;
        }

        private bool Alive(int gen)
        {
            return _run && gen == _gen;
        }

        public void Start(string url, string code, bool insecure)
        {
            Stop();
            _url = url;
            _code = code;
            _insecure = insecure;
            if (url.Length == 0 || code.Length == 0)
            {
                Status = "missing url or code";
                return;
            }
            _run = true;
            int gen = _gen;
            var t = new Thread(() => Loop(gen, url, code, insecure));
            t.IsBackground = true;
            t.Start();
        }

        public void Stop()
        {
            _gen++;
            _run = false;
            Guild = "";
            var w = _ws;
            _ws = null;
            if (w != null) w.Dispose();
            string drop;
            while (_out.TryDequeue(out drop))
            {
            }
            _outReady.Set();
            Status = "off";
        }

        public void Send(JObject o)
        {
            if (o == null || !Connected) return;
            SendRaw(o.ToString(Formatting.None));
        }

        public void SendRaw(string json)
        {
            if (string.IsNullOrEmpty(json) || !Connected) return;
            if (_out.Count > 400) return;
            _out.Enqueue(json);
            _outReady.Set();
        }

        public void Notify(string source, string text, string key = null, string a = null, string b = null)
        {
            if (!Connected) return;
            var o = new JObject();
            o["type"] = "event";
            o["source"] = source;
            o["text"] = text;
            if (key != null)
            {
                o["key"] = key;
                o["a"] = a ?? "";
                o["b"] = b ?? "";
            }
            Send(o);
        }

        private void Loop(int gen, string url, string code, bool insecure)
        {
            int backoff = 3000;
            while (Alive(gen))
            {
                var ws = new WebSocketClient();
                try
                {
                    Status = "connecting";
                    ws.Connect(url, insecure, 10000);
                    if (Alive(gen))
                    {
                        _ws = ws;
                        var hello = new JObject();
                        hello["type"] = "hello";
                        hello["code"] = code;
                        hello["mod"] = "ChatRealms";
                        hello["version"] = "0.1.0";
                        ws.SendText(hello.ToString(Formatting.None));
                        Status = "waiting for pairing";
                        backoff = 3000;
                        var writer = new Thread(() => WriteLoop(gen, ws));
                        writer.IsBackground = true;
                        writer.Start();
                        while (Alive(gen))
                        {
                            string text = ws.ReceiveText();
                            if (text == null) break;
                            Handle(gen, text);
                        }
                    }
                }
                catch (Exception e)
                {
                    if (Alive(gen)) Status = "error: " + e.Message;
                }
                ws.Dispose();
                if (_ws == ws)
                {
                    _ws = null;
                    if (Alive(gen)) Guild = "";
                }
                _outReady.Set();
                if (!Alive(gen)) break;
                for (int waited = 0; waited < backoff && Alive(gen); waited += 250) Thread.Sleep(250);
                backoff = Math.Min(backoff * 2, 30000);
            }
        }

        private void WriteLoop(int gen, WebSocketClient ws)
        {
            try
            {
                while (Alive(gen) && ws.Connected && _ws == ws)
                {
                    _outReady.WaitOne(1000);
                    string s;
                    while (Alive(gen) && ws.Connected && _ws == ws && _out.TryDequeue(out s)) ws.SendText(s);
                }
            }
            catch (Exception)
            {
                ws.Dispose();
            }
        }

        private void Handle(int gen, string text)
        {
            if (!Alive(gen)) return;
            JObject o;
            try
            {
                o = JObject.Parse(text);
            }
            catch (Exception)
            {
                return;
            }
            string type = (string)o["type"];
            if (type == "welcome")
            {
                Guild = (string)o["guild"] ?? "paired";
                Status = "paired: " + Guild;
                SnapshotWanted = true;
            }
            else if (type == "error")
            {
                Status = "error: " + (string)o["message"];
            }
            else if (type == "snapshot_request")
            {
                SnapshotWanted = true;
            }
            else if (type == "action")
            {
                if (Requests.Count > 200) return;
                var r = new RelayRequest();
                r.Id = (string)o["id"] ?? "";
                r.UserId = (string)o["userId"] ?? "";
                r.UserName = (string)o["userName"] ?? "";
                r.Kingdom = (string)o["kingdom"] ?? "";
                r.WorldId = (string)o["worldId"] ?? "";
                r.Action = (string)o["action"] ?? "";
                r.UnitId = o["unitId"] != null && o["unitId"].Type != JTokenType.Null ? (long)o["unitId"] : 0L;
                r.Args = o["args"] as JObject ?? new JObject();
                r.Lineage = o["lineage"] as JObject ?? new JObject();
                Requests.Enqueue(r);
            }
        }
    }
}
