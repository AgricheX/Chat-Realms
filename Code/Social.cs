using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChatRealms
{
    internal static class Social
    {
        private static readonly HashSet<string> Blocked = new HashSet<string> { "grin_mark", "bloodline_bond" };

        private static JObject Fail(ActCtx c, string code, JObject data = null)
        {
            return ActionHandler.Result(c.R, false, code, code, data);
        }

        private static JObject Done(ActCtx c, string msg, string kind, string name)
        {
            var d = ActionHandler.UnitInfo(c.U);
            d["kind"] = kind;
            d["meta"] = name;
            return ActionHandler.Result(c.R, true, "ok", msg, d);
        }

        public static void Register(Dictionary<string, UnitAct> d)
        {
            UnitActions.Add(d, "unit_meta", 1, false, c =>
            {
                string kind = (string)c.R.Args["kind"];
                string op = (string)c.R.Args["op"];
                string trait = (string)c.R.Args["trait"];
                bool religion = kind == "religion";
                if (!religion && kind != "culture") return Fail(c, "unknown_action");
                Actor u = c.U;
                if (op == "found")
                {
                    if (religion)
                    {
                        if (u.hasReligion() && u.religion.data.creator_id == u.getID()) return Fail(c, "already_founded");
                        Religion rel = World.world.religions.newReligion(u, true);
                        if (rel == null) return Fail(c, "no_effect");
                        u.setReligion(rel);
                        try { rel.convertSameSpeciesAroundUnit(u); } catch (Exception) { }
                        ChatRealmsMod.Announce(u.getName() + " -> " + rel.name);
                        return Done(c, "founded", kind, rel.name);
                    }
                    if (u.hasCulture() && u.culture.data.creator_id == u.getID()) return Fail(c, "already_founded");
                    Culture cul = World.world.cultures.newCulture(u, true);
                    if (cul == null) return Fail(c, "no_effect");
                    u.setCulture(cul);
                    try { cul.convertSameSpeciesAroundUnit(u); } catch (Exception) { }
                    ChatRealmsMod.Announce(u.getName() + " -> " + cul.name);
                    return Done(c, "founded", kind, cul.name);
                }
                if (op == "spread")
                {
                    City city = u.city;
                    if (city == null) return Fail(c, "no_city");
                    if (religion ? !u.hasReligion() : !u.hasCulture()) return Fail(c, "no_meta");
                    int limit = Mathf.Clamp(3 + u.data.level / 3, 3, 12);
                    int n = 0;
                    foreach (Actor a in new List<Actor>(city.units))
                    {
                        if (n >= limit) break;
                        if (a == null || !a.isAlive() || a == u) continue;
                        if (religion)
                        {
                            if (a.religion == u.religion) continue;
                            a.setReligion(u.religion);
                        }
                        else
                        {
                            if (a.culture == u.culture) continue;
                            a.setCulture(u.culture);
                        }
                        n++;
                    }
                    if (n == 0) return Fail(c, "no_effect");
                    var d2 = ActionHandler.UnitInfo(c.U);
                    d2["kind"] = kind;
                    d2["converted"] = n;
                    d2["meta"] = religion ? u.religion.name : u.culture.name;
                    return ActionHandler.Result(c.R, true, "ok", "spread", d2);
                }
                if (op == "trait_add" || op == "trait_remove")
                {
                    int rank = UnitActions.Rank(u);
                    if (rank < 4) return ActionHandler.Result(c.R, false, "role_required", "role required", new JObject { ["need"] = "leader", ["role"] = ActionHandler.RoleOf(u) });
                    if (string.IsNullOrEmpty(trait) || Blocked.Contains(trait)) return Fail(c, "unknown_trait");
                    if (religion)
                    {
                        if (!u.hasReligion()) return Fail(c, "no_meta");
                        Religion rel = u.religion;
                        if (rel.data.creator_id != u.getID() && !u.isKing()) return Fail(c, "not_meta_founder");
                        if (AssetManager.religion_traits.get(trait) == null) return Fail(c, "unknown_trait");
                        if (op == "trait_add")
                        {
                            if (rel.hasTrait(trait)) return Fail(c, "already_has_trait");
                            if (!rel.addTrait(trait, true)) return Fail(c, "no_effect");
                        }
                        else
                        {
                            if (!rel.hasTrait(trait)) return Fail(c, "no_effect");
                            rel.removeTrait(trait);
                        }
                        var dr = Done(c, "trait", kind, rel.name);
                        ((JObject)dr["data"])["trait"] = trait;
                        return dr;
                    }
                    if (!u.hasCulture()) return Fail(c, "no_meta");
                    Culture cul = u.culture;
                    if (cul.data.creator_id != u.getID() && !u.isKing()) return Fail(c, "not_meta_founder");
                    if (AssetManager.culture_traits.get(trait) == null) return Fail(c, "unknown_trait");
                    if (op == "trait_add")
                    {
                        if (cul.hasTrait(trait)) return Fail(c, "already_has_trait");
                        if (!cul.addTrait(trait, true)) return Fail(c, "no_effect");
                    }
                    else
                    {
                        if (!cul.hasTrait(trait)) return Fail(c, "no_effect");
                        cul.removeTrait(trait);
                    }
                    var dc = Done(c, "trait", kind, cul.name);
                    ((JObject)dc["data"])["trait"] = trait;
                    return dc;
                }
                return Fail(c, "unknown_action");
            });

            UnitActions.Add(d, "unit_rename", 1, false, c =>
            {
                string kind = (string)c.R.Args["kind"];
                string raw = (string)c.R.Args["name"];
                Actor u = c.U;
                long id = u.getID();
                bool king = u.isKing();
                string name = null;
                if (!string.IsNullOrEmpty(raw))
                {
                    var sb = new System.Text.StringBuilder();
                    foreach (char ch in raw)
                    {
                        if (ch >= 32 && ch != 127) sb.Append(ch);
                    }
                    name = sb.ToString().Trim();
                    if (name.Length < 2) return Fail(c, "blocked");
                    if (name.Length > 30) name = name.Substring(0, 30).Trim();
                    string lower = name.ToLowerInvariant();
                    foreach (string w in c.S.BlockedWords.Split(','))
                    {
                        string b = w.Trim().ToLowerInvariant();
                        if (b.Length > 0 && lower.Contains(b)) return Fail(c, "blocked");
                    }
                }
                if (name == null) return Fail(c, "unknown_action");
                string shown;
                if (kind == "family")
                {
                    if (!u.hasFamily()) return Fail(c, "no_meta");
                    Family f = u.family;
                    if (!king && f.data.main_founder_id_1 != id && f.data.main_founder_id_2 != id && f.data.alpha_id != id) return Fail(c, "not_meta_founder");
                    if (name != null) f.setName(name);
                    if (name != null) f.data.custom_name = true;
                    shown = f.name;
                }
                else if (kind == "clan")
                {
                    if (!u.hasClan()) return Fail(c, "no_meta");
                    Clan cl = u.clan;
                    if (!king && cl.data.chief_id != id && cl.data.founder_actor_id != id) return Fail(c, "not_meta_founder");
                    if (name != null) cl.setName(name);
                    if (name != null) cl.data.custom_name = true;
                    shown = cl.name;
                }
                else if (kind == "religion")
                {
                    if (!u.hasReligion()) return Fail(c, "no_meta");
                    Religion r = u.religion;
                    if (!king && r.data.creator_id != id) return Fail(c, "not_meta_founder");
                    if (name != null) r.setName(name);
                    if (name != null) r.data.custom_name = true;
                    shown = r.name;
                }
                else if (kind == "culture")
                {
                    if (!u.hasCulture()) return Fail(c, "no_meta");
                    Culture cu = u.culture;
                    if (!king && cu.data.creator_id != id) return Fail(c, "not_meta_founder");
                    if (name != null) cu.setName(name);
                    if (name != null) cu.data.custom_name = true;
                    shown = cu.name;
                }
                else return Fail(c, "unknown_action");
                var d2 = ActionHandler.UnitInfo(u);
                d2["kind"] = kind;
                d2["meta"] = shown;
                d2["renamed"] = name != null;
                return ActionHandler.Result(c.R, true, "ok", "renamed", d2);
            });

            UnitActions.Add(d, "unit_favorite", 1, false, c =>
            {
                c.U.data.favorite = true;
                return ActionHandler.Result(c.R, true, "ok", "favorite", ActionHandler.UnitInfo(c.U));
            });

            UnitActions.Add(d, "unit_trait", 1, false, c =>
            {
                string trait = (string)c.R.Args["trait"];
                string op = (string)c.R.Args["op"] ?? "add";
                if (string.IsNullOrEmpty(trait) || AssetManager.traits.get(trait) == null) return Fail(c, "unknown_trait");
                bool ok;
                if (op == "remove")
                {
                    if (!c.U.hasTrait(trait)) return Fail(c, "no_effect");
                    c.U.removeTrait(trait);
                    ok = true;
                }
                else
                {
                    if (c.U.hasTrait(trait)) return Fail(c, "already_has_trait");
                    ok = c.U.addTrait(trait, true);
                }
                if (!ok) return Fail(c, "no_effect");
                c.U.setStatsDirty();
                var d2 = ActionHandler.UnitInfo(c.U);
                d2["trait"] = trait;
                d2["op"] = op;
                return ActionHandler.Result(c.R, true, "ok", "trait", d2);
            });
        }
    }
}
