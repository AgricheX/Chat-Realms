using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace ChatRealms
{
    internal static class Cohesion
    {
        private const string Key = "chatrealms_cohesion";
        public const float Base = 70f;
        private static Dictionary<long, float> _values = new Dictionary<long, float>();
        private static string _world = "";
        private static float _tick;
        private static float _save;
        private static readonly Dictionary<long, float[]> _timers = new Dictionary<long, float[]>();
        private static bool _dirty;

        private static readonly Dictionary<string, float> Weights = new Dictionary<string, float>
        {
            { "gold", 1f }, { "wood", 0.3f }, { "stone", 0.3f }, { "wheat", 0.4f }, { "common_metals", 0.8f }, { "silver", 1.5f },
            { "mythril", 4f }, { "adamantine", 6f }, { "gems", 2f }, { "bones", 0.3f }, { "leather", 0.5f }, { "dragon_scales", 8f }
        };

        public static float Weight(string resource)
        {
            float w;
            return Weights.TryGetValue(resource, out w) ? w : 0f;
        }

        public static bool Known(string resource)
        {
            return Weights.ContainsKey(resource);
        }

        private static void Sync()
        {
            string id = WorldInfo.Current();
            if (id == _world) return;
            _world = id;
            _values = new Dictionary<long, float>();
            try
            {
                string raw;
                World.world.map_stats.custom_data.get(Key, out raw, "");
                if (!string.IsNullOrEmpty(raw))
                {
                    var loaded = JsonConvert.DeserializeObject<Dictionary<long, float>>(raw);
                    if (loaded != null) _values = loaded;
                }
            }
            catch (Exception)
            {
            }
        }

        public static float Get(Kingdom k)
        {
            Sync();
            float v;
            return _values.TryGetValue(k.id, out v) ? v : Base;
        }

        public static void Set(Kingdom k, float v)
        {
            Sync();
            _values[k.id] = Mathf.Clamp(v, 0f, 100f);
            _dirty = true;
        }

        public static void Add(Kingdom k, float delta)
        {
            Set(k, Get(k) + delta);
        }

        public static float Drop(Kingdom k, string resource, int amount, float scale)
        {
            int pop = Mathf.Max(1, k.getPopulationPeople());
            float loss = amount * Weight(resource) / (20f + pop * 1.5f) * 12f * scale;
            Add(k, -loss);
            return loss;
        }

        public static string Label(float v)
        {
            if (v >= 80f) return "united";
            if (v >= 60f) return "stable";
            if (v >= 45f) return "uneasy";
            if (v >= 30f) return "angry";
            if (v >= 15f) return "rebellious";
            return "collapsing";
        }

        public static void OnAction(Kingdom k, string action)
        {
            if (k == null) return;
            switch (action)
            {
                case "unit_festival": Add(k, 6f); break;
                case "unit_tribute": Add(k, 2f); break;
                case "unit_gift": Add(k, 1f); break;
                case "feast": Add(k, 10f); break;
                case "tournament": Add(k, 6f); break;
                case "rest_day": Add(k, 6f); break;
                case "harvest_blessing": Add(k, 5f); break;
                case "call_to_arms": Add(k, -4f); break;
            }
        }

        public static void Tick(Settings s)
        {
            if (World.world == null || !GameOps.WorldReady) return;
            float now = Time.realtimeSinceStartup;
            if (now - _tick < 5f) return;
            float dt = now - _tick;
            _tick = now;
            if (dt > 30f) dt = 5f;
            Sync();
            if (_dirty && now - _save > 30f)
            {
                _save = now;
                _dirty = false;
                try
                {
                    World.world.map_stats.custom_data.set(Key, JsonConvert.SerializeObject(_values));
                }
                catch (Exception)
                {
                }
            }
            var ids = new List<long>(_values.Keys);
            foreach (long id in ids)
            {
                Kingdom k = null;
                foreach (Kingdom c in GameOps.CivKingdoms())
                {
                    if (c.id == id)
                    {
                        k = c;
                        break;
                    }
                }
                if (k == null)
                {
                    _values.Remove(id);
                    continue;
                }
                float v = _values[id];
                float step = dt / 60f;
                if (v < 75f) v = Mathf.Min(75f, v + 0.6f * step);
                else if (v > 75f) v = Mathf.Max(75f, v - 0.3f * step);
                _values[id] = v;
                if (!s.CohesionEnabled) continue;
                Consequences(k, v, now);
            }
        }

        private static List<Actor> Commoners(Kingdom k, int max)
        {
            var list = new List<Actor>();
            foreach (City c in k.cities)
            {
                foreach (Actor a in new List<Actor>(c.units))
                {
                    if (a == null || !a.isAlive() || a.isKing() || a.isCityLeader() || a.data.favorite) continue;
                    list.Add(a);
                }
            }
            var picked = new List<Actor>();
            while (list.Count > 0 && picked.Count < max)
            {
                int i = UnityEngine.Random.Range(0, list.Count);
                picked.Add(list[i]);
                list.RemoveAt(i);
            }
            return picked;
        }

        private static void Consequences(Kingdom k, float v, float now)
        {
            float[] t;
            if (!_timers.TryGetValue(k.id, out t))
            {
                t = new float[3];
                _timers[k.id] = t;
            }
            if (v < 50f && now - t[0] > 30f)
            {
                t[0] = now;
                foreach (Actor a in Commoners(k, 4))
                {
                    try
                    {
                        a.changeHappiness("got_robbed");
                        if (v < 35f) a.addStatusEffect("angry");
                    }
                    catch (Exception)
                    {
                    }
                }
            }
            if (v < 35f && now - t[1] > 60f)
            {
                t[1] = now;
                int n = 0;
                foreach (Actor a in Commoners(k, 3))
                {
                    try
                    {
                        a.removeFromPreviousFaction();
                        a.setDefaultKingdom();
                        Wanderers.Guard(a);
                        n++;
                    }
                    catch (Exception)
                    {
                    }
                }
                if (n > 0) ChatRealmsMod.Announce(k.name + ": " + n + " left");
            }
            if (v < 20f && now - t[2] > 300f && k.cities.Count > 1)
            {
                t[2] = now;
                foreach (City c in new List<City>(k.cities))
                {
                    if (c == k.capital || c.leader == null || !c.leader.isAlive() || c.leader.data.favorite) continue;
                    try
                    {
                        Kingdom nk = c.makeOwnKingdom(c.leader, true);
                        if (nk != null)
                        {
                            World.world.diplomacy.startWar(k, nk, WarTypeLibrary.rebellion);
                            ChatRealmsMod.Announce(c.name + " -> " + k.name);
                            Cinema.ReportCity(c, 8f);
                            Add(k, 8f);
                        }
                    }
                    catch (Exception)
                    {
                    }
                    break;
                }
            }
            if (v <= 8f && k.king != null && k.king.isAlive())
            {
                Actor king = k.king;
                try
                {
                    ChatRealmsMod.Announce(king.getName() + " X " + k.name);
                    Cinema.Report(king.cur_transform_position, 9f, 28f);
                    k.kingLeftEvent();
                    Set(k, 35f);
                    Crown(k, king);
                }
                catch (Exception)
                {
                }
            }
        }

        private static void Crown(Kingdom k, Actor deposed)
        {
            if (k.king != null) return;
            Actor heir = SuccessionTool.getKingFromRoyalClan(k, deposed);
            if (heir == null) heir = SuccessionTool.getKingFromLeaders(k);
            if (heir == null || heir == deposed || !heir.isAlive() || heir.kingdom != k) return;
            if (heir.hasCity())
            {
                heir.stopBeingWarrior();
                if (heir.isCityLeader()) heir.city.removeLeader();
            }
            if (k.capital != null && heir.city != k.capital) heir.joinCity(k.capital);
            k.setKing(heir);
            WorldLog.logNewKing(k);
            ChatRealmsMod.Announce(heir.getName() + " -> " + k.name);
        }
    }
}
