using System;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChatRealms
{
    internal class ActionHandler
    {
        private readonly Dictionary<string, float> _last = new Dictionary<string, float>();
        private readonly Queue<float> _stamps = new Queue<float>();
        private readonly Queue<float> _twitchStamps = new Queue<float>();

        private static readonly HashSet<string> Free = new HashSet<string> { "alliance_check", "alliance_status" };

        private static readonly Dictionary<string, string> SelfEvents = new Dictionary<string, string>
        {
            { "bless", "k_bless" }, { "inspire", "k_inspire" }, { "cure", "k_cure" }, { "shield", "k_shield" },
            { "friendship", "k_friendship" }, { "rain", "k_rain" }, { "grow", "k_grow" }, { "party", "k_party" },
            { "gift_gold", "k_gold" }, { "gift_wheat", "k_wheat" }, { "reinforce", "k_reinforce" }, { "peace", "k_peace" }
        };

        private static readonly Dictionary<string, string> HostileEvents = new Dictionary<string, string>
        {
            { "curse", "k_curse" }, { "plague", "k_plague" }, { "madness", "k_madness" }, { "fire", "k_fire" },
            { "discord", "k_discord" }, { "acid", "k_acid" }, { "spite", "k_spite" }
        };

        internal static JObject Result(RelayRequest r, bool ok, string code, string message, JObject data)
        {
            var o = new JObject();
            o["type"] = "result";
            o["id"] = r.Id;
            o["ok"] = ok;
            o["code"] = code;
            o["message"] = message;
            if (data != null) o["data"] = data;
            return o;
        }

        public static JObject KingdomInfo(Kingdom k)
        {
            var o = new JObject();
            o["name"] = k.name;
            o["cities"] = k.cities.Count;
            o["population"] = k.getPopulationPeople();
            o["king"] = k.king != null ? k.king.getName() : "";
            o["color"] = ColorHex(k);
            var wars = new JArray();
            foreach (Kingdom e in GameOps.CivKingdoms())
            {
                if (e != k && World.world.wars.isInWarWith(k, e)) wars.Add(e.name);
            }
            o["wars"] = wars;
            return o;
        }

        private static string ColorHex(Kingdom k)
        {
            try
            {
                Color32 c = k.getColor().getColorMain32();
                return "#" + c.r.ToString("x2") + c.g.ToString("x2") + c.b.ToString("x2");
            }
            catch (Exception)
            {
                return "#888888";
            }
        }

        public JObject Snapshot()
        {
            var o = new JObject();
            o["type"] = "snapshot";
            o["world"] = World.world != null && World.world.map_stats != null ? World.world.map_stats.name : "";
            o["worldId"] = WorldInfo.Current();
            var arr = new JArray();
            int n = 0;
            foreach (Kingdom k in GameOps.CivKingdoms())
            {
                arr.Add(KingdomInfo(k));
                if (++n >= 40) break;
            }
            o["kingdoms"] = arr;
            return o;
        }

        private string Sanitize(string text, Settings s)
        {
            if (string.IsNullOrEmpty(text)) return null;
            var sb = new StringBuilder();
            foreach (char c in text)
            {
                if (c >= 32 && c != 127) sb.Append(c);
            }
            string t = sb.ToString().Trim();
            if (t.Length == 0) return null;
            if (t.Length > 70) t = t.Substring(0, 70);
            string lower = t.ToLowerInvariant();
            foreach (string w in s.BlockedWords.Split(','))
            {
                string b = w.Trim().ToLowerInvariant();
                if (b.Length > 0 && lower.Contains(b)) return null;
            }
            return t;
        }

        private bool Throttle(RelayRequest r, Settings s)
        {
            float now = Time.realtimeSinceStartup;
            string uid = r.UserId ?? "";
            Queue<float> q = uid.StartsWith("tw:", StringComparison.Ordinal) ? _twitchStamps : _stamps;
            while (q.Count > 0 && now - q.Peek() > 60f) q.Dequeue();
            if (q.Count >= s.ActionsPerMinute) return false;
            float last;
            if (_last.TryGetValue(uid, out last) && now - last < s.UserCooldown) return false;
            if (_last.Count > 3000) Prune(now, s.UserCooldown);
            _last[uid] = now;
            q.Enqueue(now);
            return true;
        }

        private void Prune(float now, float cooldown)
        {
            var old = new List<string>();
            foreach (KeyValuePair<string, float> p in _last)
            {
                if (now - p.Value >= cooldown) old.Add(p.Key);
            }
            foreach (string k in old) _last.Remove(k);
        }

        private static readonly HashSet<string> GeneralEvents = new HashSet<string> { "shield", "inspire", "bless", "reinforce", "cure" };

        private static Actor LiveUnit(RelayRequest r)
        {
            if (r.UnitId == 0 || World.world == null) return null;
            Actor u = World.world.units.get(r.UnitId);
            if (u == null || !u.isAlive()) return null;
            return u;
        }

        internal static string RoleOf(Actor u)
        {
            if (u.isKing()) return "king";
            if (u.isCityLeader()) return u.city != null && u.city.isCapitalCity() ? "leader_capital" : "leader";
            if (u.is_army_captain) return "general";
            if (u.isWarrior()) return "soldier";
            return "citizen";
        }

        private static JObject Denied(RelayRequest r, string need, Actor u)
        {
            var d = new JObject();
            d["need"] = need;
            d["role"] = RoleOf(u);
            return Result(r, false, "role_required", "role required", d);
        }

        public JObject Handle(RelayRequest r, Settings s)
        {
            try
            {
                if (!GameOps.Loaded) return Result(r, false, "world_unavailable", "world not ready", null);
                if (r.Action == "track_units")
                {
                    JArray ids = r.Args["ids"] as JArray;
                    int n = 0;
                    if (ids != null)
                    {
                        foreach (JToken t in ids)
                        {
                            Actor tu = World.world.units.get((long)t);
                            if (tu != null && tu.isAlive())
                            {
                                Wanderers.Track(tu.data.id);
                                n++;
                            }
                        }
                    }
                    return Result(r, true, "ok", "tracked", new JObject { ["count"] = n });
                }
                if (!string.IsNullOrEmpty(r.WorldId))
                {
                    string current = WorldInfo.Current();
                    if (current.Length > 0 && current != r.WorldId) return Result(r, false, "world_mismatch", "world changed", new JObject { ["worldId"] = current });
                }
                if (r.Action == "screenshot") return StartShot(r, s);
                if (!GameOps.WorldReady) return Result(r, false, "world_unavailable", "world not ready", null);
                if (r.Action == "kingdoms")
                {
                    var d = new JObject();
                    d["kingdoms"] = Snapshot()["kingdoms"];
                    return Result(r, true, "ok", "kingdoms", d);
                }
                if (r.Action == "status")
                {
                    Kingdom sk = GameOps.FindKingdom(r.Kingdom);
                    if (sk == null) return Result(r, false, "kingdom_gone", "kingdom not found", null);
                    return Result(r, true, "ok", "status", KingdomInfo(sk));
                }
                if (r.Action == "validate_kingdom")
                {
                    Kingdom vk = GameOps.FindKingdom((string)r.Args["name"]);
                    if (vk == null) return Result(r, false, "kingdom_not_found", "kingdom not found", null);
                    return Result(r, true, "ok", "kingdom ok", KingdomInfo(vk));
                }
                if (Intel.Handles(r.Action)) return Intel.Handle(r, s, LiveUnit(r));
                if (Powers.Handles(r.Action)) return Powers.Handle(r);
                if (r.Action == "unit_succeed") return Succeed(r);
                if (r.Action == "unit_release")
                {
                    Actor old = LiveUnit(r);
                    if (old != null) old.data.favorite = false;
                    return Result(r, true, "ok", "released", null);
                }
                Actor unit = LiveUnit(r);
                if (s.UnitMode && r.UnitId != 0 && unit == null) return Result(r, false, "unit_dead", "unit is gone", null);
                if (!Free.Contains(r.Action) && !Throttle(r, s)) return Result(r, false, "rate_limited", "slow down", null);
                if (unit != null) Wanderers.Track(unit.data.id);
                if (unit != null && s.UnitMode && Wanderers.Wild(unit))
                {
                    Wanderers.Guard(unit);
                    if (r.Action == "unit_migrate") return Migrate(r, unit);
                    if (r.Action.StartsWith("unit_") && r.Action != "unit_create" && r.Action != "unit_enlist" && r.Action != "unit_family" && r.Action != "unit_clan" && r.Action != "unit_renegade")
                    {
                        return HandleUnit(r, s, null, unit);
                    }
                    return Result(r, false, "no_kingdom", "unit has no kingdom", UnitInfo(unit));
                }
                Kingdom k = unit != null && unit.kingdom != null && unit.kingdom.isCiv() ? unit.kingdom : GameOps.FindKingdom(r.Kingdom);
                if (k == null) return Result(r, false, "kingdom_gone", "kingdom not found", null);
                if (r.Action == "unit_migrate") return Migrate(r, unit);
                if (r.Action == "unit_renegade") return Renegade(r, s, unit);
                if (r.Action == "unit_create") return CreateUnit(r, s, k, unit);
                if (r.Action == "unit_enlist") return Enlist(r, s, unit);
                if (r.Action == "unit_family") return FoundFamily(r, s, unit);
                if (r.Action == "unit_clan") return FoundClan(r, s, unit);
                if (r.Action.StartsWith("unit_")) return HandleUnit(r, s, k, unit);
                return HandleKingdom(r, s, k, unit);
            }
            catch (Exception e)
            {
                ChatRealmsMod.LogWarning("action " + r.Action + ": " + e.Message);
                return Result(r, false, "internal_error", e.Message, null);
            }
        }

        private JObject StartShot(RelayRequest r, Settings s)
        {
            if (!s.AllowScreenshots) return Result(r, false, "disabled", "screenshots disabled", null);
            if (Shots.Busy) return Result(r, false, "busy", "busy", null);
            if (!Throttle(r, s)) return Result(r, false, "rate_limited", "slow down", null);
            string mode = (string)r.Args["mode"] ?? "screen";
            if (mode != "unit" && mode != "city" && mode != "kingdom" && mode != "world") mode = "screen";
            Vector3? focus = null;
            float zoom = 0f;
            var info = new JObject();
            Actor u = LiveUnit(r);
            if (s.UnitMode)
            {
                if (u == null) return Result(r, false, r.UnitId != 0 ? "unit_dead" : "unit_required", "unit required", null);
                int need = mode == "unit" ? 1 : (mode == "city" ? 3 : 5);
                if (UnitActions.Rank(u) < need) return Denied(r, UnitActions.NeedName(need), u);
            }
            float aspect = Screen.height > 0 ? (float)Screen.width / Screen.height : 1.78f;
            if (mode == "unit")
            {
                if (u == null) return Result(r, false, "unit_dead", "unit is gone", null);
                focus = u.cur_transform_position;
                zoom = 10f;
                info = UnitInfo(u);
            }
            else if (mode == "city")
            {
                if (u == null) return Result(r, false, "unit_required", "unit required", null);
                City c = u.current_zone != null ? u.current_zone.city : null;
                if (c == null || c.kingdom != u.kingdom || c.zones.Count == 0) return Result(r, false, "not_in_city", "not in your city", null);
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (TileZone z in c.zones)
                {
                    WorldTile t = z.centerTile;
                    if (t == null) continue;
                    minX = Mathf.Min(minX, t.x);
                    maxX = Mathf.Max(maxX, t.x);
                    minY = Mathf.Min(minY, t.y);
                    maxY = Mathf.Max(maxY, t.y);
                }
                if (minX > maxX) return Result(r, false, "not_in_city", "not in your city", null);
                focus = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0f);
                zoom = Mathf.Max((maxY - minY) / 2f + 12f, (maxX - minX) / 2f / aspect + 12f);
                zoom = Mathf.Clamp(zoom, 20f, 120f);
                info = new JObject();
                info["city"] = c.name;
                info["kingdom"] = c.kingdom.name;
            }
            else if (mode == "kingdom")
            {
                Kingdom k = GameOps.FindKingdom(r.Kingdom);
                if (u != null && u.kingdom != null && u.kingdom.isCiv()) k = u.kingdom;
                if (k == null || k.cities.Count == 0) return Result(r, false, "kingdom_gone", "kingdom not found", null);
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                foreach (City c in k.cities)
                {
                    WorldTile t = c.getTile();
                    if (t == null) continue;
                    minX = Mathf.Min(minX, t.x);
                    maxX = Mathf.Max(maxX, t.x);
                    minY = Mathf.Min(minY, t.y);
                    maxY = Mathf.Max(maxY, t.y);
                }
                if (minX > maxX) return Result(r, false, "kingdom_gone", "kingdom not found", null);
                focus = new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0f);
                zoom = Mathf.Max((maxY - minY) / 2f + 30f, (maxX - minX) / 2f / aspect + 30f);
                zoom = Mathf.Clamp(zoom, 40f, 160f);
                info = KingdomInfo(k);
            }
            else if (mode == "world")
            {
                focus = new Vector3(MapBox.width / 2f, MapBox.height / 2f, 0f);
                zoom = Mathf.Max(MapBox.height / 2f + 4f, MapBox.width / 2f / aspect + 4f);
            }
            Shots.Begin(r, mode, focus, zoom, info);
            return null;
        }

        private JObject HandleKingdom(RelayRequest r, Settings s, Kingdom k, Actor unit)
        {
            bool roles = s.UnitMode;
            if (roles && unit == null)
            {
                return Result(r, false, r.UnitId != 0 ? "unit_dead" : "unit_required", "unit required", null);
            }
            if (unit != null && Fav(r)) unit.data.favorite = true;

            string target = (string)r.Args["target"];
            string cityName = (string)r.Args["city"];
            var et = new EventTarget { K = k };
            City chosen = null;
            if (!string.IsNullOrEmpty(cityName))
            {
                City c = GameOps.FindCity(cityName);
                if (c != null && c.kingdom == k) chosen = c;
            }

            string eventId;
            bool hostile = HostileEvents.ContainsKey(r.Action);
            bool selfEvent = SelfEvents.ContainsKey(r.Action);
            bool royal = RoyalEvents.Ids.Contains(r.Action);
            bool alliance = r.Action.StartsWith("alliance_");
            bool kingOnly = hostile || r.Action == "declare_war" || r.Action == "peace" || royal || (alliance && r.Action != "alliance_status");

            if (roles)
            {
                bool king = unit.isKing();
                bool leader = unit.isCityLeader();
                if (r.Action == "rebel")
                {
                    if (!leader || unit.city == null || unit.city.kingdom != k || unit.city.isCapitalCity()) return Denied(r, "leader_noncapital", unit);
                }
                else if (kingOnly)
                {
                    if (!king) return Denied(r, "king", unit);
                }
                else if (selfEvent)
                {
                    bool general = unit.is_army_captain && GeneralEvents.Contains(r.Action);
                    if (!king && !leader && !general) return Denied(r, GeneralEvents.Contains(r.Action) ? "leader_or_general" : "leader", unit);
                    if (!king) chosen = unit.city;
                }
            }
            if (chosen != null) et.C = chosen;

            if (royal)
            {
                JObject rr = RoyalEvents.Run(r, k);
                if (rr != null && (bool)rr["ok"])
                {
                    Cohesion.OnAction(k, r.Action);
                    Cinema.ReportKingdom(k, 5f);
                }
                return rr;
            }
            if (alliance) return Alliances.Handle(r, s, k);

            if (r.Action == "rebel")
            {
                if (!s.AllowWars) return Result(r, false, "disabled", "wars disabled", null);
                if (!roles) return Result(r, false, "unit_required", "unit required", null);
                City rc = unit.city;
                Kingdom old = k;
                if (old.cities.Count < 2) return Result(r, false, "cannot_rebel", "only city", null);
                Kingdom nk = rc.makeOwnKingdom(unit, true);
                if (nk == null) return Result(r, false, "no_effect", "no effect", null);
                World.world.diplomacy.startWar(old, nk, WarTypeLibrary.rebellion);
                ChatRealmsMod.Announce(rc.name + " -> " + old.name);
                Cinema.ReportCity(rc, 9f);
                var rd = new JObject();
                rd["kingdom"] = nk.name;
                rd["target"] = old.name;
                rd["city"] = rc.name;
                return Result(r, true, "ok", "rebellion", rd);
            }

            if (r.Action == "declare_war")
            {
                if (!s.AllowWars) return Result(r, false, "disabled", "wars disabled", null);
                Kingdom other = GameOps.FindKingdom(target);
                if (other == null) return Result(r, false, "target_not_found", "target not found", null);
                if (other == k) return Result(r, false, "invalid_target", "cannot target self", null);
                if (Alliances.Allied(k, other)) return Result(r, false, "ally_target", "target is an ally", null);
                if (!GameOps.DeclareWar(k, other)) return Result(r, false, "already_at_war", "already at war", null);
                ChatRealmsMod.Announce(k.name + " -> " + other.name);
                Cinema.ReportKingdom(k, 9f);
                var d = new JObject();
                d["target"] = other.name;
                return Result(r, true, "ok", "war declared", d);
            }

            if (SelfEvents.TryGetValue(r.Action, out eventId))
            {
                return RunEvent(r, eventId, et, null);
            }

            if (HostileEvents.TryGetValue(r.Action, out eventId))
            {
                if (!s.AllowDiscordDestructive) return Result(r, false, "disabled", "destructive actions disabled", null);
                Kingdom other = GameOps.FindKingdom(target);
                if (other == null) return Result(r, false, "target_not_found", "target not found", null);
                if (other == k) return Result(r, false, "invalid_target", "cannot target self", null);
                if (Alliances.Allied(k, other)) return Result(r, false, "ally_target", "target is an ally", null);
                return RunEvent(r, eventId, new EventTarget { K = other }, other.name);
            }

            return Result(r, false, "unknown_action", "unknown action", null);
        }

        private JObject RunEvent(RelayRequest r, string eventId, EventTarget et, string targetName)
        {
            EventDef def = Events.All.Find(e => e.Id == eventId);
            if (def == null) return Result(r, false, "unknown_action", "unknown event", null);
            bool ok = def.Run(et);
            if (!ok) return Result(r, false, "no_effect", "no effect", null);
            var d = new JObject();
            d["event"] = def.Id;
            if (targetName != null) d["target"] = targetName;
            ChatRealmsMod.Announce(r.UserName + ": " + def.En + (targetName != null ? " -> " + targetName : ""));
            if (et != null && et.K != null) Cinema.ReportKingdom(et.K, 4.5f);
            return Result(r, true, "ok", def.En, d);
        }

        private JObject CreateUnit(RelayRequest r, Settings s, Kingdom k, Actor existing)
        {
            if (!s.UnitMode) return Result(r, false, "disabled", "unit mode disabled", null);
            if (existing != null) return Result(r, false, "already_has_unit", "already has a unit", UnitInfo(existing));
            string name = Sanitize((string)r.Args["name"], s);
            if (name == null) return Result(r, false, "blocked", "name rejected", null);
            if (name.Length > 24) name = name.Substring(0, 24).Trim();
            string species = k.getSpecies();
            if (string.IsNullOrEmpty(species)) return Result(r, false, "no_effect", "no species", null);
            City home = k.capital;
            if (home == null && k.cities.Count > 0) home = k.cities[0];
            if (home == null) return Result(r, false, "kingdom_gone", "kingdom has no city", null);
            WorldTile spot = null;
            for (int i = 0; i < 8; i++)
            {
                WorldTile t = GameOps.Around(home.getTile(), 4);
                if (t != null && !t.is_liquid)
                {
                    spot = t;
                    break;
                }
            }
            if (spot == null) spot = home.getTile();
            Actor a = World.world.units.spawnNewUnit(species, spot, false, false, 0f, null, false, true);
            if (a == null) return Result(r, false, "no_effect", "spawn failed", null);
            string sex = (string)r.Args["sex"];
            if (a.data.sex != ActorSex.None)
            {
                if (sex == "male") a.data.sex = ActorSex.Male;
                else if (sex == "female") a.data.sex = ActorSex.Female;
            }
            a.addTrait("immortal");
            a.setName(name);
            a.joinCity(home);
            if (Fav(r)) a.data.favorite = true;
            ChatRealmsMod.Announce(r.UserName + " -> " + name);
            return Result(r, true, "ok", "created", UnitInfo(a));
        }

        private static bool Fav(RelayRequest r)
        {
            JToken t = r.Args != null ? r.Args["noFav"] : null;
            return !(t != null && t.Type == JTokenType.Boolean && (bool)t);
        }

        private JObject Succeed(RelayRequest r)
        {
            Actor live = LiveUnit(r);
            if (live != null)
            {
                if (Fav(r)) live.data.favorite = true;
                return Result(r, true, "ok", "alive", UnitInfo(live));
            }
            var exclude = new HashSet<long>();
            JArray ex = r.Lineage["exclude"] as JArray;
            if (ex != null)
            {
                foreach (JToken t in ex) exclude.Add((long)t);
            }
            long familyId = r.Lineage["family"] != null && r.Lineage["family"].Type != JTokenType.Null ? (long)r.Lineage["family"] : 0L;
            long clanId = r.Lineage["clan"] != null && r.Lineage["clan"].Type != JTokenType.Null ? (long)r.Lineage["clan"] : 0L;
            long preferred = r.Lineage["heir"] != null && r.Lineage["heir"].Type != JTokenType.Null ? (long)r.Lineage["heir"] : 0L;
            Actor heir = GameOps.FindHeir(r.UnitId, familyId, clanId, exclude, preferred);
            if (heir == null) return Result(r, false, "no_heir", "no heir", null);
            if (Fav(r)) heir.data.favorite = true;
            JObject d = UnitInfo(heir);
            d["heir"] = true;
            ChatRealmsMod.Announce(r.UserName + " -> " + heir.getName());
            return Result(r, true, "ok", "heir", d);
        }

        private JObject Enlist(RelayRequest r, Settings s, Actor u)
        {
            if (!s.UnitMode) return Result(r, false, "disabled", "unit mode disabled", null);
            if (u == null) return Result(r, false, "unit_dead", "unit is gone", null);
            if (u.isKing() || u.isCityLeader()) return Result(r, false, "not_for_rulers", "rulers cannot enlist", null);
            City city = u.city;
            if (city == null) return Result(r, false, "no_city", "unit has no city", null);
            if (u.is_army_captain) return Result(r, false, "already_general", "already general", UnitInfo(u));
            if (Fav(r)) u.data.favorite = true;
            bool enlisted = false;
            if (!u.isWarrior())
            {
                city.makeWarrior(u);
                enlisted = true;
            }
            city.checkArmyExistence();
            float chance = Mathf.Min(0.5f, 0.12f + 0.01f * u.data.level);
            bool promoted = UnityEngine.Random.value < chance;
            if (promoted)
            {
                if (city.hasArmy())
                {
                    u.setArmy(city.getArmy());
                    city.getArmy().setCaptain(u);
                }
                else
                {
                    World.world.armies.newArmy(u, city);
                }
                ChatRealmsMod.Announce(u.getName() + " -> General");
            }
            JObject d = UnitInfo(u);
            d["enlisted"] = enlisted;
            d["promoted"] = promoted;
            return Result(r, true, "ok", "enlisted", d);
        }

        private JObject FoundFamily(RelayRequest r, Settings s, Actor u)
        {
            if (!s.UnitMode) return Result(r, false, "disabled", "unit mode disabled", null);
            if (u == null) return Result(r, false, "unit_dead", "unit is gone", null);
            if (u.hasFamily()) return Result(r, false, "already_has_family", "already has family", UnitInfo(u));
            if (Fav(r)) u.data.favorite = true;
            Family f = World.world.families.newFamily(u, u.current_tile, null);
            if (f == null) return Result(r, false, "no_effect", "no effect", null);
            ChatRealmsMod.Announce(u.getName() + " -> " + f.name);
            return Result(r, true, "ok", "family", UnitInfo(u));
        }

        private JObject FoundClan(RelayRequest r, Settings s, Actor u)
        {
            if (!s.UnitMode) return Result(r, false, "disabled", "unit mode disabled", null);
            if (u == null) return Result(r, false, "unit_dead", "unit is gone", null);
            if (u.hasClan()) return Result(r, false, "already_has_clan", "already has clan", UnitInfo(u));
            if (Fav(r)) u.data.favorite = true;
            Clan c = World.world.clans.newClan(u, true);
            if (c == null) return Result(r, false, "no_effect", "no effect", null);
            ChatRealmsMod.Announce(u.getName() + " -> " + c.name);
            return Result(r, true, "ok", "clan", UnitInfo(u));
        }

        private static readonly HashSet<string> WanderOk = new HashSet<string>
        {
            "unit_rest", "unit_eat", "unit_coffee", "unit_meditate", "unit_party", "unit_sing", "unit_pray", "unit_outdoors",
            "unit_study", "unit_combat", "unit_aim", "unit_run", "unit_endure", "unit_fence", "unit_gather", "unit_equip"
        };

        private JObject Migrate(RelayRequest r, Actor u)
        {
            if (u == null) return Result(r, false, "unit_dead", "unit is gone", null);
            Kingdom target = GameOps.FindKingdom((string)r.Args["name"]);
            if (target == null) return Result(r, false, "kingdom_not_found", "kingdom not found", null);
            if (u.isKing() || u.isCityLeader()) return Result(r, false, "not_for_rulers", "rulers cannot migrate", null);
            if (u.kingdom == target) return Result(r, false, "already_member", "already member", UnitInfo(u));
            City city = target.capital;
            if (city == null && target.cities.Count > 0) city = target.cities[0];
            if (city == null) return Result(r, false, "kingdom_gone", "kingdom has no city", null);
            Wanderers.Release(u);
            u.removeFromPreviousFaction();
            u.joinCity(city);
            if (Fav(r)) u.data.favorite = true;
            WorldTile tile = city.getTile();
            if (tile != null) u.goTo(tile);
            ChatRealmsMod.Announce(u.getName() + " -> " + target.name);
            JObject d = UnitInfo(u);
            d["migrated"] = true;
            return Result(r, true, "ok", "migrated", d);
        }

        private JObject Renegade(RelayRequest r, Settings s, Actor u)
        {
            if (!s.UnitMode) return Result(r, false, "disabled", "unit mode disabled", null);
            if (u == null) return Result(r, false, "unit_dead", "unit is gone", null);
            if (u.isKing() || u.isCityLeader()) return Result(r, false, "not_for_rulers", "rulers cannot leave", null);
            u.removeFromPreviousFaction();
            u.setDefaultKingdom();
            Wanderers.Guard(u);
            if (Fav(r)) u.data.favorite = true;
            ChatRealmsMod.Announce(u.getName() + " -> ?");
            return Result(r, true, "ok", "renegade", UnitInfo(u));
        }

        private JObject HandleUnit(RelayRequest r, Settings s, Kingdom k, Actor u)
        {
            JObject res = HandleUnitCore(r, s, k, u);
            if (res != null && (bool)res["ok"] && u != null && res["data"] is JObject)
            {
                Cohesion.OnAction(k, r.Action);
                Cinema.ReportUnit(u, r.Action);
                string ev = Needs.OnAction(u, r.Action);
                if (ev != null) ((JObject)res["data"])["need"] = ev;
            }
            return res;
        }

        private JObject HandleUnitCore(RelayRequest r, Settings s, Kingdom k, Actor u)
        {
            if (!s.UnitMode) return Result(r, false, "disabled", "unit mode disabled", null);
            if (u == null) return Result(r, false, "unit_dead", "unit is gone", null);
            if (Fav(r)) u.data.favorite = true;
            bool wanderer = k == null;

            UnitAct act;
            if (UnitActions.Table.TryGetValue(r.Action, out act))
            {
                if (wanderer && !WanderOk.Contains(r.Action)) return Result(r, false, "no_kingdom", "unit has no kingdom", UnitInfo(u));
                if (act.Need > 1 && UnitActions.Rank(u) < act.Need) return Denied(r, UnitActions.NeedName(act.Need), u);
                Actor target = null;
                if (act.NeedsTarget)
                {
                    long tid = r.Args["targetUnitId"] != null && r.Args["targetUnitId"].Type != JTokenType.Null ? (long)r.Args["targetUnitId"] : 0L;
                    target = tid != 0 ? World.world.units.get(tid) : null;
                    if (target == null || !target.isAlive()) return Result(r, false, "target_dead", "target is gone", null);
                }
                return act.Run(new ActCtx { R = r, S = s, U = u, K = k, T = target });
            }

            switch (r.Action)
            {
                case "unit_info":
                    return Result(r, true, "ok", "info", UnitInfo(u));
                case "unit_say":
                    string text = Sanitize((string)r.Args["text"], s);
                    if (text == null) return Result(r, false, "blocked", "text rejected", null);
                    ChatRealmsMod.Announce(u.getName() + ": " + text);
                    return Result(r, true, "ok", "said", UnitInfo(u));
                case "unit_heal":
                    u.restoreHealthPercent(0.5f);
                    return Result(r, true, "ok", "healed", UnitInfo(u));
                case "unit_train":
                    u.addExperience(15);
                    return Result(r, true, "ok", "trained", UnitInfo(u));
                case "unit_inspire":
                    u.addStatusEffect("inspired");
                    return Result(r, true, "ok", "inspired", UnitInfo(u));
                case "unit_explore":
                    for (int i = 0; i < 6; i++)
                    {
                        WorldTile t = GameOps.Around(u.current_tile, 25);
                        if (t != null && !t.is_liquid)
                        {
                            u.goTo(t);
                            return Result(r, true, "ok", "exploring", UnitInfo(u));
                        }
                    }
                    return Result(r, false, "no_path", "no path found", null);
            }
            return Result(r, false, "unknown_action", "unknown action", null);
        }

        internal static JObject UnitInfo(Actor a)
        {
            var o = new JObject();
            o["unitId"] = a.data.id;
            o["name"] = a.getName();
            o["age"] = a.getAge();
            o["sex"] = a.data.sex == ActorSex.Female ? "female" : (a.data.sex == ActorSex.Male ? "male" : "none");
            o["health"] = Math.Round(a.getHealthRatio() * 100f);
            o["city"] = a.city != null ? a.city.name : "";
            o["king"] = a.isKing();
            o["role"] = RoleOf(a);
            o["family"] = a.hasFamily() ? a.family.id : 0L;
            o["familyName"] = a.hasFamily() ? a.family.name : "";
            o["clan"] = a.hasClan() ? a.clan.id : 0L;
            o["clanName"] = a.hasClan() ? a.clan.name : "";
            o["kingdom"] = a.kingdom != null && a.kingdom.isCiv() ? a.kingdom.name : "";
            return o;
        }
    }
}
