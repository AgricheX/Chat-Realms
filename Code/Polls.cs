using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChatRealms
{
    internal class PollManager
    {
        public bool Voting { get; private set; }
        public List<EventDef> Options = new List<EventDef>();
        public int[] Counts = new int[0];
        public float TimeLeft;
        public string ResultText = "";
        public float ResultUntil;
        public EventTarget Target;
        public bool RoleplayPoll;

        private readonly Dictionary<string, int> _votes = new Dictionary<string, int>();
        private float _untilNext = 15f;
        private readonly System.Random _rng = new System.Random();

        public void StartNow()
        {
            _untilNext = 0f;
        }

        public void SkipCurrent()
        {
            Voting = false;
            _untilNext = 5f;
        }

        public void Tick(float dt, Settings s, TwitchChat chat)
        {
            ChatMessage m;
            while (chat.Inbox.TryDequeue(out m))
            {
                if (Voting) HandleVote(m);
            }

            if (!s.TwitchEnabled)
            {
                Voting = false;
                return;
            }

            if (!Voting)
            {
                if (!GameOps.WorldReady) return;
                _untilNext -= dt;
                if (_untilNext <= 0f) Begin(s);
                return;
            }

            TimeLeft -= dt;
            if (TimeLeft <= 0f)
            {
                TimeLeft = 0f;
                if (GameOps.WorldReady) Finish(s);
            }
        }

        private void Begin(Settings s)
        {
            _votes.Clear();
            RoleplayPoll = s.Roleplay;
            Target = null;
            var pool = new List<EventDef>();
            if (RoleplayPoll)
            {
                Target = GameOps.ResolveRoleplay(s.RoleplayTarget);
                if (Target == null || Target.K == null)
                {
                    _untilNext = 10f;
                    return;
                }
            }
            foreach (EventDef e in Events.All)
            {
                if (e.Destructive && !s.AllowDestructive) continue;
                if (RoleplayPoll && (!e.KingdomScope || e.ChaosOnly)) continue;
                if (e.Id == "k_war" && GameOps.CivKingdoms().Count < 2) continue;
                pool.Add(e);
            }
            int want = Math.Min(s.PollOptions, pool.Count);
            if (want < 2)
            {
                _untilNext = 10f;
                return;
            }
            Options = new List<EventDef>();
            while (Options.Count < want)
            {
                int i = _rng.Next(pool.Count);
                Options.Add(pool[i]);
                pool.RemoveAt(i);
            }
            Counts = new int[Options.Count];
            TimeLeft = s.PollDuration;
            Voting = true;
            ChatRealmsMod.Announce(s.Portuguese ? "Votacao aberta! Digite o numero no chat." : "Vote is open! Type the number in chat.");
        }

        private void HandleVote(ChatMessage m)
        {
            string t = m.Text.Trim();
            if (t.StartsWith("!")) t = t.Substring(1);
            if (t.StartsWith("vote ", StringComparison.OrdinalIgnoreCase)) t = t.Substring(5).Trim();
            int space = t.IndexOf(' ');
            if (space > 0) t = t.Substring(0, space);
            int n;
            if (!int.TryParse(t, out n)) return;
            n -= 1;
            if (n < 0 || n >= Options.Count) return;
            int prev;
            if (_votes.TryGetValue(m.User, out prev)) Counts[prev]--;
            _votes[m.User] = n;
            Counts[n]++;
        }

        private void Finish(Settings s)
        {
            Voting = false;
            _untilNext = s.PollInterval;
            int best = -1;
            var winners = new List<int>();
            for (int i = 0; i < Counts.Length; i++)
            {
                if (Counts[i] > best)
                {
                    best = Counts[i];
                    winners.Clear();
                    winners.Add(i);
                }
                else if (Counts[i] == best)
                {
                    winners.Add(i);
                }
            }
            int pick = winners[_rng.Next(winners.Count)];
            EventDef ev = Options[pick];
            EventTarget target = Target;
            if (ev.KingdomScope && (target == null || target.K == null || target.K.cities.Count == 0))
            {
                target = new EventTarget { K = GameOps.RandomKingdom() };
            }
            bool ok = false;
            try
            {
                if (!ev.KingdomScope || (target != null && target.K != null)) ok = ev.Run(target ?? new EventTarget());
            }
            catch (Exception e)
            {
                ChatRealmsMod.LogWarning("event " + ev.Id + ": " + e.Message);
            }
            string label = ev.KingdomScope && target != null ? " -> " + target.Label : "";
            string name = ev.Name(s.Portuguese);
            ResultText = (s.Portuguese ? "Resultado: " : "Result: ") + name + label + " (" + best + ")" + (ok ? "" : (s.Portuguese ? " [falhou]" : " [failed]"));
            ResultUntil = Time.unscaledTime + 12f;
            ChatRealmsMod.Announce(ResultText);
            ChatRealmsMod.I.Relay.Notify("twitch", name + label);
        }
    }
}
