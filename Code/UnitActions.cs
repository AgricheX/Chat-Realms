using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChatRealms
{
    internal class ActCtx
    {
        public RelayRequest R;
        public Settings S;
        public Actor U;
        public Kingdom K;
        public Actor T;
    }

    internal class UnitAct
    {
        public int Need;
        public bool NeedsTarget;
        public Func<ActCtx, JObject> Run;
    }

    internal static class UnitActions
    {
        public static readonly Dictionary<string, UnitAct> Table = Build();

        internal static void Add(Dictionary<string, UnitAct> d, string id, int need, bool target, Func<ActCtx, JObject> run)
        {
            d[id] = new UnitAct { Need = need, NeedsTarget = target, Run = run };
        }

        public static int Rank(Actor u)
        {
            if (u.isKing()) return 5;
            if (u.isCityLeader()) return 4;
            if (u.is_army_captain) return 3;
            if (u.isWarrior()) return 2;
            return 1;
        }

        public static string NeedName(int need)
        {
            switch (need)
            {
                case 2: return "soldier";
                case 3: return "general";
                case 4: return "leader";
                default: return "king";
            }
        }

        private static JObject Ok(ActCtx c, string msg)
        {
            return ActionHandler.Result(c.R, true, "ok", msg, ActionHandler.UnitInfo(c.U));
        }

        private static JObject Fail(ActCtx c, string code)
        {
            return ActionHandler.Result(c.R, false, code, code, null);
        }

        private static JObject WithTrait(ActCtx c, string msg, string trait)
        {
            JObject d = ActionHandler.UnitInfo(c.U);
            if (trait != null) d["trait"] = trait;
            return ActionHandler.Result(c.R, true, "ok", msg, d);
        }

        private static string TryTrait(Actor a, float chance, params string[] ids)
        {
            if (UnityEngine.Random.value >= chance) return null;
            string id = ids[UnityEngine.Random.Range(0, ids.Length)];
            try
            {
                if (a.addTrait(id)) return id;
            }
            catch (Exception)
            {
            }
            return null;
        }

        private static List<Actor> CityUnits(City c, Func<Actor, bool> filter, int max)
        {
            var list = new List<Actor>();
            if (c == null) return list;
            foreach (Actor a in new List<Actor>(c.units))
            {
                if (a == null || !a.isAlive()) continue;
                if (filter != null && !filter(a)) continue;
                list.Add(a);
                if (list.Count >= max) break;
            }
            return list;
        }

        private static int Status(List<Actor> units, string id)
        {
            int n = 0;
            foreach (Actor a in units)
            {
                if (a.addStatusEffect(id)) n++;
            }
            return n;
        }

        private static bool Allied(Kingdom a, Kingdom b)
        {
            if (a == b) return true;
            Alliance al = a.getAlliance();
            return al != null && al.hasKingdom(b);
        }

        private static City FindOwnCity(ActCtx c)
        {
            string name = (string)c.R.Args["city"];
            City city = GameOps.FindCity(name);
            if (city == null || city.kingdom == null || !Allied(c.K, city.kingdom)) return null;
            return city;
        }

        private static JObject Work(ActCtx c, string msg, string res1, int amt1, string res2, int amt2)
        {
            City city = c.U.city;
            if (city == null) return Fail(c, "no_city");
            int got = city.addResourcesToRandomStockpile(res1, amt1);
            if (res2 != null) got += city.addResourcesToRandomStockpile(res2, amt2);
            if (got <= 0) return Fail(c, "no_effect");
            c.U.addExperience(2);
            return Ok(c, msg);
        }

        private static Dictionary<string, UnitAct> Build()
        {
            var d = new Dictionary<string, UnitAct>();

            Add(d, "unit_rest", 1, false, c =>
            {
                c.U.restoreHealthPercent(0.3f);
                c.U.addStatusEffect("had_good_dream");
                return Ok(c, "rest");
            });
            Add(d, "unit_eat", 1, false, c =>
            {
                c.U.addNutritionFromEating(100, false, true);
                return Ok(c, "eat");
            });
            Add(d, "unit_coffee", 1, false, c =>
            {
                c.U.addStatusEffect("caffeinated");
                return Ok(c, "coffee");
            });
            Add(d, "unit_meditate", 1, false, c =>
            {
                c.U.addStatusEffect("inspired");
                return WithTrait(c, "meditate", TryTrait(c.U, 0.2f, "strong_minded"));
            });
            Add(d, "unit_party", 1, false, c =>
            {
                c.U.addStatusEffect("festive_spirit");
                return Ok(c, "party");
            });
            Add(d, "unit_sing", 1, false, c =>
            {
                c.U.addStatusEffect("singing");
                return Ok(c, "sing");
            });
            Add(d, "unit_pray", 1, false, c =>
            {
                c.U.addStatusEffect("shield");
                return Ok(c, "pray");
            });
            Add(d, "unit_outdoors", 1, false, c =>
            {
                c.U.restoreHealthPercent(0.15f);
                c.U.addStatusEffect("sleeping");
                return Ok(c, "outdoors");
            });

            Add(d, "unit_study", 1, false, c =>
            {
                c.U.addExperience(8);
                return WithTrait(c, "study", TryTrait(c.U, 0.1f, "genius"));
            });
            Add(d, "unit_combat", 1, false, c =>
            {
                c.U.addExperience(25);
                return WithTrait(c, "combat", TryTrait(c.U, 0.12f, "strong", "agile"));
            });
            Add(d, "unit_aim", 1, false, c =>
            {
                c.U.addExperience(12);
                return WithTrait(c, "aim", TryTrait(c.U, 0.15f, "eagle_eyed"));
            });
            Add(d, "unit_run", 1, false, c =>
            {
                c.U.addExperience(10);
                return WithTrait(c, "run", TryTrait(c.U, 0.15f, "fast"));
            });
            Add(d, "unit_endure", 1, false, c =>
            {
                c.U.addExperience(12);
                return WithTrait(c, "endure", TryTrait(c.U, 0.15f, "tough"));
            });
            Add(d, "unit_fence", 1, false, c =>
            {
                c.U.addExperience(15);
                return WithTrait(c, "fence", TryTrait(c.U, 0.15f, "dodge", "block", "dash", "backstep"));
            });

            Add(d, "unit_go", 1, false, c =>
            {
                City city = FindOwnCity(c);
                if (city == null) return Fail(c, "city_not_found");
                WorldTile t = city.getTile();
                if (t == null) return Fail(c, "no_path");
                c.U.goTo(t);
                return Ok(c, "go");
            });
            Add(d, "unit_patrol", 1, false, c =>
            {
                WorldTile origin = c.U.city != null ? c.U.city.getTile() : c.U.current_tile;
                for (int i = 0; i < 8; i++)
                {
                    WorldTile t = GameOps.Around(origin, 15);
                    if (t != null && !t.is_liquid)
                    {
                        c.U.goTo(t);
                        return Ok(c, "patrol");
                    }
                }
                return Fail(c, "no_path");
            });
            Add(d, "unit_home", 1, false, c =>
            {
                if (c.U.city == null) return Fail(c, "no_city");
                WorldTile t = c.U.city.getTile();
                if (t == null) return Fail(c, "no_path");
                c.U.goTo(t);
                return Ok(c, "home");
            });
            Add(d, "unit_camp", 1, false, c =>
            {
                c.U.restoreHealthPercent(0.25f);
                c.U.addStatusEffect("on_guard");
                return Ok(c, "camp");
            });

            Add(d, "unit_work", 1, false, c =>
            {
                string[] pool = { "wheat", "wood", "stone" };
                return Work(c, "work", pool[UnityEngine.Random.Range(0, pool.Length)], 15, null, 0);
            });
            Add(d, "unit_mine", 1, false, c => Work(c, "mine", "stone", 20, "gold", 8));
            Add(d, "unit_hunt", 1, false, c => Work(c, "hunt", "meat", 25, null, 0));
            Add(d, "unit_wood", 1, false, c => Work(c, "wood", "wood", 25, null, 0));
            Add(d, "unit_tribute", 1, false, c =>
            {
                JObject res = Work(c, "tribute", "gold", 20, null, 0);
                if ((bool)res["ok"]) c.U.changeHappiness("just_gave_gift");
                return res;
            });
            Add(d, "unit_found", 4, false, c =>
            {
                for (int i = 0; i < 40; i++)
                {
                    WorldTile t = GameOps.Around(c.U.current_tile, 30);
                    if (t == null || t.is_liquid || t.zone == null) continue;
                    if (t.zone.hasCity() || !t.zone.isGoodForNewCity(c.U)) continue;
                    City made = World.world.cities.buildNewCity(c.U, t.zone);
                    if (made == null) continue;
                    ChatRealmsMod.Announce(c.U.getName() + " -> " + made.name);
                    JObject data = ActionHandler.UnitInfo(c.U);
                    data["city"] = made.name;
                    return ActionHandler.Result(c.R, true, "ok", "founded", data);
                }
                return Fail(c, "no_place");
            });

            Add(d, "unit_gift", 1, true, c =>
            {
                City city = c.T.city;
                if (city == null) return Fail(c, "no_city");
                if (city.addResourcesToRandomStockpile("gold", 30) <= 0) return Fail(c, "no_effect");
                c.T.changeHappiness("just_received_gift");
                c.U.changeHappiness("just_gave_gift");
                return PairOk(c, "gift");
            });
            Add(d, "unit_love", 1, true, c =>
            {
                string err = PairCheck(c, true);
                if (err != null) return Fail(c, err);
                c.U.becomeLoversWith(c.T);
                c.U.changeHappiness("just_kissed");
                c.T.changeHappiness("just_kissed");
                c.U.addStatusEffect("fell_in_love");
                c.T.addStatusEffect("fell_in_love");
                return PairOk(c, "love");
            });
            Add(d, "unit_friend", 1, true, c =>
            {
                string err = PairCheck(c, false);
                if (err != null) return Fail(c, err);
                c.U.setBestFriend(c.T, true);
                c.T.setBestFriend(c.U, true);
                return PairOk(c, "friend");
            });
            Add(d, "unit_duel", 1, true, c =>
            {
                string err = PairCheck(c, false);
                if (err != null) return Fail(c, err);
                if (Duels.Busy(c.U) || Duels.Busy(c.T)) return Fail(c, "duel_busy");
                Duels.Start(c.U, c.T);
                return PairOk(c, "duel");
            });
            Add(d, "unit_marry", 1, true, c =>
            {
                string err = PairCheck(c, true);
                if (err != null) return Fail(c, err);
                c.U.becomeLoversWith(c.T);
                if (!c.U.hasFamily() && !c.T.hasFamily()) World.world.families.newFamily(c.U, c.U.current_tile, c.T);
                else if (c.U.hasFamily()) c.T.setFamily(c.U.family);
                else c.U.setFamily(c.T.family);
                c.U.changeHappiness("just_kissed");
                c.T.changeHappiness("just_kissed");
                return PairOk(c, "marry");
            });

            Add(d, "unit_warcry", 2, false, c =>
            {
                int n = Status(CityUnits(c.U.city, a => a.isWarrior() || a.isKing(), 60), "motivated");
                return n > 0 ? Ok(c, "warcry") : Fail(c, "no_effect");
            });
            Add(d, "unit_watch", 2, false, c =>
            {
                int n = Status(CityUnits(c.U.city, a => a.isWarrior(), 60), "on_guard");
                return n > 0 ? Ok(c, "watch") : Fail(c, "no_effect");
            });
            Add(d, "unit_troop_shield", 3, false, c =>
            {
                int n = Status(CityUnits(c.U.city, a => a.isWarrior(), 60), "shield");
                return n > 0 ? Ok(c, "troop_shield") : Fail(c, "no_effect");
            });
            Add(d, "unit_recruit", 3, false, c =>
            {
                City city = c.U.city;
                if (city == null) return Fail(c, "no_city");
                var picks = CityUnits(city, a => a.isAdult() && !a.isWarrior() && !a.isKing() && !a.isCityLeader(), 4);
                foreach (Actor a in picks) city.makeWarrior(a);
                return picks.Count > 0 ? Ok(c, "recruit") : Fail(c, "no_effect");
            });
            Add(d, "unit_march", 3, false, c =>
            {
                City dest = FindOwnCity(c);
                if (dest == null) return Fail(c, "city_not_found");
                var troops = CityUnits(c.U.city, a => a.isWarrior(), 60);
                int n = 0;
                foreach (Actor a in troops)
                {
                    WorldTile t = GameOps.Around(dest.getTile(), 4);
                    if (t != null && !t.is_liquid)
                    {
                        a.goTo(t);
                        n++;
                    }
                }
                return n > 0 ? Ok(c, "march") : Fail(c, "no_effect");
            });

            Add(d, "unit_festival", 4, false, c =>
            {
                City city = c.U.city;
                if (city == null) return Fail(c, "no_city");
                int n = Status(CityUnits(city, null, 80), "festive_spirit");
                GameOps.Drop(city.getTile(), "fireworks", 4, 5);
                ChatRealmsMod.Announce(city.name + " -> " + c.U.getName());
                return n > 0 ? Ok(c, "festival") : Fail(c, "no_effect");
            });
            Add(d, "unit_tax", 4, false, c =>
            {
                City city = c.U.city;
                if (city == null) return Fail(c, "no_city");
                if (city.addResourcesToRandomStockpile("gold", 40) <= 0) return Fail(c, "no_effect");
                foreach (Actor a in CityUnits(city, x => x.isAdult(), 40)) a.changeHappiness("paid_tax");
                return Ok(c, "tax");
            });
            Add(d, "unit_heir", 1, false, c =>
            {
                string name = ((string)c.R.Args["name"] ?? "").Trim();
                if (name.Length == 0) return Fail(c, "no_heir");
                foreach (Actor kid in c.U.getChildren())
                {
                    if (kid != null && kid.isAlive() && string.Equals(kid.getName(), name, StringComparison.OrdinalIgnoreCase))
                    {
                        JObject data = ActionHandler.UnitInfo(c.U);
                        data["heirId"] = kid.data.id;
                        data["heirName"] = kid.getName();
                        return ActionHandler.Result(c.R, true, "ok", "heir", data);
                    }
                }
                return Fail(c, "no_heir");
            });

            Items.Register(d);
            Social.Register(d);
            Powers.Register(d);
            ClanOps.Register(d);

            return d;
        }

        private static string PairCheck(ActCtx c, bool lovers)
        {
            if (c.T == null || !c.T.isAlive()) return "target_dead";
            if (c.T == c.U) return "invalid_target";
            if (lovers)
            {
                if (!c.U.isAdult() || !c.T.isAdult()) return "not_adult";
                if (c.U.hasLover() || c.T.hasLover()) return "has_lover";
            }
            return null;
        }

        private static JObject PairOk(ActCtx c, string msg)
        {
            JObject d = ActionHandler.UnitInfo(c.U);
            d["partner"] = c.T.getName();
            ChatRealmsMod.Announce(c.U.getName() + " & " + c.T.getName());
            return ActionHandler.Result(c.R, true, "ok", msg, d);
        }
    }
}
