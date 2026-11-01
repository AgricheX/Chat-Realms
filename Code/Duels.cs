using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChatRealms
{
    internal static class Duels
    {
        private const float YieldRatio = 0.25f;
        private const float Limit = 150f;

        private class Duel
        {
            public long A;
            public long B;
            public float Age;
        }

        private static readonly List<Duel> _list = new List<Duel>();
        private static float _tick;

        public static bool Busy(Actor a)
        {
            long id = a.getID();
            foreach (Duel d in _list)
            {
                if (d.A == id || d.B == id) return true;
            }
            return false;
        }

        public static void Start(Actor a, Actor b)
        {
            _list.Add(new Duel { A = a.getID(), B = b.getID() });
            Engage(a, b);
            Engage(b, a);
        }

        private static void Engage(Actor who, Actor foe)
        {
            try
            {
                if (!who.isInAggroList(foe)) who.addAggro(foe);
                if (!who.has_attack_target || who.attack_target != foe) who.setAttackTarget(foe);
            }
            catch (Exception)
            {
            }
        }

        private static void Calm(Actor a)
        {
            if (a == null || !a.isAlive()) return;
            try
            {
                a.finishAngryStatus();
                a.clearAttackTarget();
            }
            catch (Exception)
            {
            }
        }

        private static float Ratio(Actor a)
        {
            return a.data.health / (float)Mathf.Max(1, a.getMaxHealth());
        }

        private static void Close(Actor a, Actor b)
        {
            Calm(a);
            Calm(b);
        }

        private static void Finish(Actor winner, Actor loser, bool yielded)
        {
            Close(winner, loser);
            if (loser != null && loser.isAlive())
            {
                loser.data.health = Mathf.Max(loser.data.health, Mathf.RoundToInt(loser.getMaxHealth() * 0.3f));
            }
            if (winner != null && winner.isAlive()) winner.addExperience(10);
            bool pt = ChatRealmsMod.Instance.Cfg.Portuguese;
            string wn = winner != null ? winner.getName() : "?";
            string ln = loser != null ? loser.getName() : "?";
            string text;
            if (yielded) text = pt ? ln + " desistiu do duelo e " + wn + " venceu" : ln + " yielded the duel and " + wn + " won";
            else text = pt ? wn + " venceu o duelo contra " + ln : wn + " won the duel against " + ln;
            ChatRealmsMod.Announce(text);
            ChatRealmsMod.Instance.Relay.Notify("duel", text, yielded ? "duel_yield" : "duel_win", wn, ln);
        }

        public static void Tick(float dt)
        {
            if (_list.Count == 0) return;
            _tick += dt;
            if (_tick < 0.5f) return;
            float step = _tick;
            _tick = 0f;
            for (int i = _list.Count - 1; i >= 0; i--)
            {
                Duel d = _list[i];
                d.Age += step;
                Actor a = World.world.units.get(d.A);
                Actor b = World.world.units.get(d.B);
                bool aOk = a != null && a.isAlive();
                bool bOk = b != null && b.isAlive();
                if (!aOk || !bOk)
                {
                    _list.RemoveAt(i);
                    if (aOk) Finish(a, b, false);
                    else if (bOk) Finish(b, a, false);
                    continue;
                }
                float ra = Ratio(a);
                float rb = Ratio(b);
                if (ra <= YieldRatio || rb <= YieldRatio)
                {
                    _list.RemoveAt(i);
                    Actor loser = ra <= rb ? a : b;
                    Actor winner = loser == a ? b : a;
                    Finish(winner, loser, true);
                    continue;
                }
                if (d.Age >= Limit)
                {
                    _list.RemoveAt(i);
                    Close(a, b);
                    bool pt = ChatRealmsMod.Instance.Cfg.Portuguese;
                    string text = pt ? "Duelo entre " + a.getName() + " e " + b.getName() + " terminou empatado" : "Duel between " + a.getName() + " and " + b.getName() + " ended in a draw";
                    ChatRealmsMod.Announce(text);
                    ChatRealmsMod.Instance.Relay.Notify("duel", text, "duel_draw", a.getName(), b.getName());
                    continue;
                }
                Engage(a, b);
                Engage(b, a);
            }
        }
    }
}
