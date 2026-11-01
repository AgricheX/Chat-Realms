using System;
using Newtonsoft.Json.Linq;
using NeoModLoader.api;
using UnityEngine;

namespace ChatRealms
{
    public class ChatRealmsMod : BasicMod<ChatRealmsMod>
    {
        internal Settings Cfg;
        internal PollManager Polls;
        internal TwitchChat Twitch;
        internal RelayClient Relay;
        internal ActionHandler Actions;
        private float _snapshotTimer;
        private string _world = "";
        private float _pingTimer;
        private bool _showStatus;

        protected override void OnModLoad()
        {
            Cfg = new Settings(GetConfig());
            Polls = new PollManager();
            Twitch = new TwitchChat();
            Relay = new RelayClient();
            Actions = new ActionHandler();
            Application.runInBackground = true;
            LogInfo("ChatRealms loaded");
        }

        internal static void Announce(string text)
        {
            ModUi.Toast(text);
        }

        private void Update()
        {
            if (Cfg == null) return;
            float dt = Time.unscaledDeltaTime;

            if (Input.GetKeyDown(KeyCode.F8)) Polls.StartNow();
            if (Input.GetKeyDown(KeyCode.F9)) Polls.SkipCurrent();
            if (Input.GetKeyDown(KeyCode.F10)) _showStatus = !_showStatus;
            if (Input.GetKeyDown(KeyCode.F7)) Cinema.Toggle();

            string channel = Cfg.TwitchChannel;
            bool wantTwitch = Cfg.TwitchEnabled && channel.Length > 0;
            if (wantTwitch && (!Twitch.Running || !string.Equals(Twitch.Channel, channel.TrimStart('#'), StringComparison.OrdinalIgnoreCase)))
            {
                Twitch.Start(channel);
            }
            else if (!wantTwitch && Twitch.Running)
            {
                Twitch.Stop();
            }
            Polls.Tick(dt, Cfg, Twitch);

            bool wantRelay = Cfg.RelayEnabled && Cfg.RelayUrl.Length > 0 && Cfg.PairingCode.Length > 0;
            if (wantRelay && !Relay.SameConfig(Cfg.RelayUrl, Cfg.PairingCode, Cfg.InsecureTls))
            {
                Relay.Start(Cfg.RelayUrl, Cfg.PairingCode, Cfg.InsecureTls);
            }
            else if (!wantRelay && Relay.Status != "off")
            {
                Relay.Stop();
            }

            if (GameOps.WorldReady)
            {
                string wid = WorldInfo.Current();
                if (wid != _world)
                {
                    _world = wid;
                    Relay.SnapshotWanted = true;
                    WorldInfo.ApplyRules(Cfg);
                }
            }
            ProcessRelay(dt);
            if (GameOps.WorldReady)
            {
                Wanderers.Tick(dt);
                Cohesion.Tick(Cfg);
                Duels.Tick(dt);
                Cinema.Tick(dt);
            }
            CinemaTab.Tick(dt, Cfg.Portuguese);
            ModUi.Tick(Cfg, Polls, Twitch, Relay, _showStatus, dt);
        }

        private void ProcessRelay(float dt)
        {
            int budget = 4;
            RelayRequest r;
            while (budget-- > 0 && Relay.Requests.TryDequeue(out r))
            {
                JObject res = Actions.Handle(r, Cfg);
                if (res != null) Relay.Send(res);
            }
            if (!Relay.Connected) return;
            _snapshotTimer += dt;
            _pingTimer += dt;
            if (Relay.SnapshotWanted || _snapshotTimer > 45f)
            {
                Relay.SnapshotWanted = false;
                _snapshotTimer = 0f;
                if (GameOps.WorldReady) Relay.Send(Actions.Snapshot());
            }
            if (_pingTimer > 20f)
            {
                _pingTimer = 0f;
                var p = new JObject();
                p["type"] = "ping";
                Relay.Send(p);
            }
        }

        private void OnApplicationQuit()
        {
            if (Twitch != null) Twitch.Stop();
            if (Relay != null) Relay.Stop();
        }
    }
}
