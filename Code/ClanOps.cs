using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChatRealms
{
    internal static class ClanOps
    {
        private static JObject Fail(ActCtx c, string code, JObject data = null)
        {
            return ActionHandler.Result(c.R, false, code, code, data);
        }

        private static bool Manager(Actor u, Clan cl)
        {
            long id = u.getID();
            return u.isKing() || cl.data.chief_id == id || cl.data.founder_actor_id == id;
        }

        private static Actor Find(Clan cl, string query)
        {
            if (string.IsNullOrEmpty(query)) return null;
            string q = query.Trim().ToLowerInvariant();
            Actor partial = null;
            foreach (Actor a in cl.units)
            {
                if (a == null || !a.isAlive()) continue;
                string n = a.getName().ToLowerInvariant();
                if (n == q) return a;
                if (partial == null && n.Contains(q)) partial = a;
            }
            return partial;
        }

        private static string Clean(string raw, int max, string blocked)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            var sb = new System.Text.StringBuilder();
            foreach (char ch in raw)
            {
                if (ch >= 32 && ch != 127) sb.Append(ch);
            }
            string t = sb.ToString().Trim();
            if (t.Length < 2) return null;
            if (t.Length > max) t = t.Substring(0, max).Trim();
            string lower = t.ToLowerInvariant();
            foreach (string w in blocked.Split(','))
            {
                string b = w.Trim().ToLowerInvariant();
                if (b.Length > 0 && lower.Contains(b)) return null;
            }
            return t;
        }

        private static JObject Info(ActCtx c, Clan cl)
        {
            var d = ActionHandler.UnitInfo(c.U);
            int members = 0;
            foreach (Actor a in cl.units)
            {
                if (a != null && a.isAlive()) members++;
            }
            Actor chief = cl.getChief();
            d["clan"] = cl.name;
            d["members"] = members;
            d["max"] = cl.getMaxMembers();
            d["full"] = cl.isFull();
            d["chief"] = chief != null ? chief.getName() : "";
            d["motto"] = cl.data.motto ?? "";
            d["manager"] = Manager(c.U, cl);
            var cities = new JArray();
            foreach (City ci in cl.getCities()) cities.Add(ci.name);
            d["cities"] = cities;
            var kingdoms = new JArray();
            foreach (Kingdom k in cl.getKingdoms()) kingdoms.Add(k.name);
            d["kingdoms"] = kingdoms;
            return ActionHandler.Result(c.R, true, "ok", "clan", d);
        }

        public static void Register(Dictionary<string, UnitAct> d)
        {
            UnitActions.Add(d, "unit_clan_manage", 1, false, c =>
            {
                string op = (string)c.R.Args["op"] ?? "info";
                Actor u = c.U;
                if (!u.hasClan()) return Fail(c, "no_clan");
                Clan cl = u.clan;
                if (op == "info") return Info(c, cl);
                if (op == "leave")
                {
                    if (cl.data.chief_id == u.getID()) return Fail(c, "chief_cannot_leave");
                    string old = cl.name;
                    u.forgetClan();
                    var dl = ActionHandler.UnitInfo(u);
                    dl["clan"] = old;
                    return ActionHandler.Result(c.R, true, "ok", "left", dl);
                }
                if (!Manager(u, cl)) return Fail(c, "not_clan_manager");
                if (op == "recruit")
                {
                    City city = u.city;
                    if (city == null) return Fail(c, "no_city");
                    if (cl.isFull()) return Fail(c, "clan_full");
                    int want = Mathf.Clamp(c.R.Args["count"] != null ? (int)c.R.Args["count"] : 3, 1, 10);
                    int n = 0;
                    foreach (Actor a in new List<Actor>(city.units))
                    {
                        if (n >= want || cl.isFull()) break;
                        if (a == null || !a.isAlive() || a == u || a.hasClan() || !a.isAdult()) continue;
                        if (a.asset != u.asset) continue;
                        a.setClan(cl);
                        n++;
                    }
                    if (n == 0) return Fail(c, "no_effect");
                    var dr = Info(c, cl);
                    ((JObject)dr["data"])["recruited"] = n;
                    ChatRealmsMod.Announce(cl.name + " +" + n);
                    return dr;
                }
                if (op == "expel" || op == "chief")
                {
                    Actor t = Find(cl, (string)c.R.Args["target"]);
                    if (t == null) return Fail(c, "member_not_found");
                    if (op == "expel")
                    {
                        if (t == u) return Fail(c, "invalid_target");
                        if (t.getID() == cl.data.chief_id) return Fail(c, "is_chief");
                        t.forgetClan();
                        var de = Info(c, cl);
                        ((JObject)de["data"])["target"] = t.getName();
                        ChatRealmsMod.Announce(cl.name + " - " + t.getName());
                        return de;
                    }
                    if (t == u && cl.data.chief_id == u.getID()) return Fail(c, "invalid_target");
                    if (!t.isAdult()) return Fail(c, "invalid_target");
                    cl.setChief(t);
                    var dc = Info(c, cl);
                    ((JObject)dc["data"])["target"] = t.getName();
                    ChatRealmsMod.Announce(cl.name + " -> " + t.getName());
                    return dc;
                }
                if (op == "motto")
                {
                    string text = Clean((string)c.R.Args["text"], 60, c.S.BlockedWords);
                    if (text == null) return Fail(c, "blocked");
                    cl.data.motto = text;
                    var dm = Info(c, cl);
                    return dm;
                }
                return Fail(c, "unknown_action");
            });
        }
    }
}
