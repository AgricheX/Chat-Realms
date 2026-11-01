using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChatRealms
{
    internal static class Powers
    {
        public static bool Handles(string action)
        {
            return action == "world_roles" || action == "city_pops";
        }

        public static JObject Handle(RelayRequest r)
        {
            var d = new JObject();
            if (r.Action == "world_roles")
            {
                var map = new JObject();
                JArray ids = r.Args["ids"] as JArray;
                if (ids != null)
                {
                    foreach (JToken t in ids)
                    {
                        long id = (long)t;
                        Actor a = World.world.units.get(id);
                        var o = new JObject();
                        bool alive = a != null && a.isAlive();
                        o["alive"] = alive;
                        if (alive)
                        {
                            o["role"] = ActionHandler.RoleOf(a);
                            o["kingdom"] = a.kingdom != null && a.kingdom.isCiv() ? a.kingdom.name : "";
                        }
                        map[id.ToString()] = o;
                    }
                }
                d["units"] = map;
                return ActionHandler.Result(r, true, "ok", "roles", d);
            }
            var pops = new JObject();
            JArray names = r.Args["names"] as JArray;
            if (names != null)
            {
                foreach (JToken t in names)
                {
                    string n = (string)t;
                    City c = GameOps.FindCity(n);
                    var o = new JObject();
                    o["alive"] = c != null;
                    if (c != null)
                    {
                        o["population"] = c.getPopulationPeople();
                        o["kingdom"] = c.kingdom != null ? c.kingdom.name : "";
                    }
                    pops[n] = o;
                }
            }
            d["cities"] = pops;
            return ActionHandler.Result(r, true, "ok", "cities", d);
        }

        private static JObject Fail(ActCtx c, string code, JObject data = null)
        {
            return ActionHandler.Result(c.R, false, code, code, data);
        }

        private static JObject Denied(ActCtx c, string need)
        {
            return ActionHandler.Result(c.R, false, "role_required", "role required", new JObject { ["need"] = need, ["role"] = ActionHandler.RoleOf(c.U) });
        }

        private static int Pop(Kingdom k)
        {
            return Mathf.Max(1, k.getPopulationPeople());
        }

        private static int Stock(IEnumerable<City> cities, string res)
        {
            int total = 0;
            foreach (City c in cities) total += c.getResourcesAmount(res);
            return total;
        }

        private static int TakeFrom(IEnumerable<City> cities, string res, int want)
        {
            int taken = 0;
            foreach (City c in cities)
            {
                if (taken >= want) break;
                int have = c.getResourcesAmount(res);
                int n = Mathf.Min(have, want - taken);
                if (n <= 0) continue;
                c.takeResource(res, n);
                taken += n;
            }
            return taken;
        }

        private static List<City> Ordered(Kingdom k)
        {
            var list = new List<City>(k.cities);
            if (k.capital != null)
            {
                list.Remove(k.capital);
                list.Insert(0, k.capital);
            }
            return list;
        }

        private static List<Actor> People(Kingdom k, int max)
        {
            var list = new List<Actor>();
            foreach (City c in k.cities)
            {
                foreach (Actor a in new List<Actor>(c.units))
                {
                    if (a == null || !a.isAlive()) continue;
                    list.Add(a);
                    if (list.Count >= max) return list;
                }
            }
            return list;
        }

        private static bool RunEvent(string id, Kingdom k)
        {
            EventDef def = Events.All.Find(e => e.Id == id);
            return def != null && def.Run(new EventTarget { K = k });
        }

        public static void Register(Dictionary<string, UnitAct> d)
        {
            UnitActions.Add(d, "unit_treasury", 1, false, c =>
            {
                string op = (string)c.R.Args["op"] ?? "list";
                string res = (string)c.R.Args["resource"];
                int amount = Mathf.Clamp(c.R.Args["amount"] != null ? (int)c.R.Args["amount"] : 0, 0, 500);
                Kingdom k = c.K;
                Actor u = c.U;
                if (k == null) return Fail(c, "no_kingdom");
                int rank = UnitActions.Rank(u);
                var d2 = ActionHandler.UnitInfo(u);
                if (op == "list")
                {
                    if (rank < 4) return Denied(c, "leader");
                    IEnumerable<City> scope = rank >= 5 ? (IEnumerable<City>)k.cities : (u.city != null ? new List<City> { u.city } : new List<City>());
                    var stock = new JObject();
                    foreach (string id in new[] { "gold", "wood", "stone", "wheat", "common_metals", "silver", "mythril", "adamantine", "gems", "bones", "leather" })
                    {
                        stock[id] = Stock(scope, id);
                    }
                    d2["stock"] = stock;
                    d2["scope"] = rank >= 5 ? "kingdom" : "city";
                    d2["cohesion"] = Mathf.Round(Cohesion.Get(k));
                    return ActionHandler.Result(c.R, true, "ok", "stock", d2);
                }
                if (string.IsNullOrEmpty(res) || !Cohesion.Known(res)) return Fail(c, "unknown_resource");
                if (amount <= 0) return Fail(c, "invalid_amount");
                if (op == "take")
                {
                    if (!u.isKing()) return Denied(c, "king");
                    int taken = TakeFrom(Ordered(k), res, amount);
                    if (taken <= 0) return Fail(c, "empty_stock");
                    Cohesion.Drop(k, res, taken, 1f);
                    d2["resource"] = res;
                    d2["amount"] = taken;
                    d2["cohesion"] = Mathf.Round(Cohesion.Get(k));
                    ChatRealmsMod.Announce(u.getName() + " - " + taken + " " + res);
                    return ActionHandler.Result(c.R, true, "ok", "taken", d2);
                }
                if (op == "city_take")
                {
                    if (rank < 4) return Denied(c, "leader");
                    City city = u.city;
                    if (city == null || city.kingdom != k) return Fail(c, "not_in_city");
                    int taken = TakeFrom(new List<City> { city }, res, amount);
                    if (taken <= 0) return Fail(c, "empty_stock");
                    Cohesion.Drop(k, res, taken, 0.6f);
                    d2["resource"] = res;
                    d2["amount"] = taken;
                    d2["cohesion"] = Mathf.Round(Cohesion.Get(k));
                    return ActionHandler.Result(c.R, true, "ok", "taken", d2);
                }
                if (op == "give")
                {
                    City city = u.city;
                    if (city == null || city.kingdom != k) return Fail(c, "not_in_city");
                    int added = city.addResourcesToRandomStockpile(res, amount);
                    if (added <= 0) return Fail(c, "no_effect");
                    Cohesion.Add(k, added * Cohesion.Weight(res) / (20f + Pop(k) * 1.5f) * 6f);
                    d2["resource"] = res;
                    d2["amount"] = added;
                    return ActionHandler.Result(c.R, true, "ok", "given", d2);
                }
                return Fail(c, "unknown_action");
            });

            UnitActions.Add(d, "unit_produce", 4, false, c =>
            {
                City city = c.U.city;
                if (city == null || c.K == null || city.kingdom != c.K) return Fail(c, "not_in_city");
                string res = (string)c.R.Args["resource"];
                if (res != "wood" && res != "stone" && res != "wheat" && res != "gold" && res != "common_metals") return Fail(c, "unknown_resource");
                int pop = Mathf.Max(5, city.getPopulationPeople());
                int per = res == "gold" ? Mathf.Max(8, pop / 2) : (res == "common_metals" ? Mathf.Max(6, pop / 3) : Mathf.Max(15, pop));
                int added = city.addResourcesToRandomStockpile(res, per);
                if (added <= 0) return Fail(c, "no_effect");
                c.U.addExperience(3);
                Cohesion.Add(c.K, 1.5f);
                var d2 = ActionHandler.UnitInfo(c.U);
                d2["resource"] = res;
                d2["amount"] = added;
                return ActionHandler.Result(c.R, true, "ok", "produced", d2);
            });

            UnitActions.Add(d, "unit_decree", 5, false, c =>
            {
                Kingdom k = c.K;
                Actor u = c.U;
                if (k == null) return Fail(c, "no_kingdom");
                string decree = (string)c.R.Args["decree"];
                var d2 = ActionHandler.UnitInfo(u);
                d2["decree"] = decree;
                switch (decree)
                {
                    case "festival":
                        {
                            int n = 0;
                            foreach (Actor a in People(k, 80))
                            {
                                if (a.addStatusEffect("festive_spirit")) n++;
                            }
                            RunEvent("k_party", k);
                            Cohesion.Add(k, 20f);
                            d2["affected"] = n;
                            break;
                        }
                    case "harvest":
                        {
                            int total = 0;
                            foreach (City city in k.cities) total += city.addResourcesToRandomStockpile("wheat", 20 + city.getPopulationPeople());
                            if (total <= 0) return Fail(c, "no_effect");
                            Cohesion.Add(k, 4f);
                            d2["amount"] = total;
                            break;
                        }
                    case "recruit_all":
                    case "conscription":
                        {
                            int per = decree == "recruit_all" ? 3 : 8;
                            int n = 0;
                            foreach (City city in new List<City>(k.cities))
                            {
                                int made = 0;
                                foreach (Actor a in new List<Actor>(city.units))
                                {
                                    if (made >= per) break;
                                    if (a == null || !a.isAlive() || a.isKing() || a.isCityLeader() || a.isWarrior() || !a.isAdult()) continue;
                                    city.makeWarrior(a);
                                    made++;
                                }
                                n += made;
                                city.checkArmyExistence();
                            }
                            if (n == 0) return Fail(c, "no_effect");
                            if (decree == "conscription") Cohesion.Add(k, -8f);
                            d2["affected"] = n;
                            break;
                        }
                    case "heal_all":
                        {
                            int n = 0;
                            foreach (Actor a in People(k, 200))
                            {
                                a.restoreHealthPercent(0.5f);
                                n++;
                            }
                            RunEvent("k_cure", k);
                            Cohesion.Add(k, 3f);
                            d2["affected"] = n;
                            break;
                        }
                    case "great_blessing":
                        {
                            RunEvent("k_bless", k);
                            RunEvent("k_shield", k);
                            RunEvent("k_inspire", k);
                            RunEvent("k_cure", k);
                            Cohesion.Add(k, 10f);
                            break;
                        }
                    case "adopt_religion":
                        {
                            if (!u.hasReligion()) return Fail(c, "no_meta");
                            k.setReligion(u.religion);
                            foreach (City city in k.cities) city.setReligion(u.religion);
                            d2["meta"] = u.religion.name;
                            d2["founded"] = u.religion.data.creator_id == u.getID();
                            Cohesion.Add(k, 5f);
                            ChatRealmsMod.Announce(k.name + " -> " + u.religion.name);
                            break;
                        }
                    case "adopt_culture":
                        {
                            if (!u.hasCulture()) return Fail(c, "no_meta");
                            k.setCulture(u.culture);
                            foreach (City city in k.cities) city.setCulture(u.culture);
                            d2["meta"] = u.culture.name;
                            d2["founded"] = u.culture.data.creator_id == u.getID();
                            Cohesion.Add(k, 5f);
                            ChatRealmsMod.Announce(k.name + " -> " + u.culture.name);
                            break;
                        }
                    case "subspecies_trait":
                        {
                            string trait = (string)c.R.Args["trait"];
                            if (string.IsNullOrEmpty(trait) || AssetManager.subspecies_traits.get(trait) == null || trait == "grin_mark") return Fail(c, "unknown_trait");
                            if (!u.hasSubspecies()) return Fail(c, "no_meta");
                            Subspecies sp = u.subspecies;
                            if (sp.hasTrait(trait)) return Fail(c, "already_has_trait");
                            if (!sp.addTrait(trait, true)) return Fail(c, "no_effect");
                            d2["trait"] = trait;
                            d2["meta"] = sp.name;
                            ChatRealmsMod.Announce(k.name + " ~ " + sp.name);
                            break;
                        }
                    default:
                        return Fail(c, "unknown_action");
                }
                return ActionHandler.Result(c.R, true, "ok", "decree", d2);
            });
        }
    }
}
