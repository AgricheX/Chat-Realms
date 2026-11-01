using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChatRealms
{
    internal static class RoyalEvents
    {
        public static readonly HashSet<string> Ids = new HashSet<string>
        {
            "feast", "tournament", "rest_day", "harvest_blessing", "call_to_arms", "pilgrimage", "royal_birth", "merchant", "royal_wedding"
        };

        private static List<Actor> People(Kingdom k, Func<Actor, bool> filter, int max)
        {
            var list = new List<Actor>();
            foreach (City c in k.cities)
            {
                foreach (Actor a in new List<Actor>(c.units))
                {
                    if (a == null || !a.isAlive()) continue;
                    if (filter != null && !filter(a)) continue;
                    list.Add(a);
                    if (list.Count >= max) return list;
                }
            }
            return list;
        }

        public static JObject Run(RelayRequest r, Kingdom k)
        {
            int n = 0;
            switch (r.Action)
            {
                case "feast":
                    foreach (Actor a in People(k, null, 150))
                    {
                        a.addNutritionFromEating(100, false, true);
                        a.changeHappiness("just_ate");
                        n++;
                    }
                    break;
                case "tournament":
                    foreach (Actor a in People(k, x => x.isAdult(), 100))
                    {
                        a.addStatusEffect("motivated");
                        a.addExperience(5);
                        n++;
                    }
                    break;
                case "rest_day":
                    foreach (Actor a in People(k, null, 150))
                    {
                        a.restoreHealthPercent(0.3f);
                        a.addStatusEffect("had_good_dream");
                        n++;
                    }
                    break;
                case "harvest_blessing":
                    n = GameOps.DropOn(new EventTarget { K = k }, "life_seed", 5, 6, 3) ? 1 : 0;
                    break;
                case "call_to_arms":
                    foreach (City c in k.cities)
                    {
                        int made = 0;
                        foreach (Actor a in new List<Actor>(c.units))
                        {
                            if (made >= 5) break;
                            if (a == null || !a.isAlive() || !a.isAdult() || a.isWarrior() || a.isKing() || a.isCityLeader()) continue;
                            c.makeWarrior(a);
                            made++;
                            n++;
                        }
                    }
                    break;
                case "pilgrimage":
                    if (k.capital != null)
                    {
                        foreach (Actor a in People(k, x => x.isAdult() && !x.isWarrior(), 30))
                        {
                            WorldTile t = GameOps.Around(k.capital.getTile(), 6);
                            if (t != null && !t.is_liquid)
                            {
                                a.goTo(t);
                                n++;
                            }
                        }
                    }
                    break;
                case "royal_birth":
                    n = RoyalBirth(k) ? 1 : 0;
                    break;
                case "merchant":
                    n = GameOps.Gift(new EventTarget { K = k }, "gold", 80) > 0 ? 1 : 0;
                    break;
                case "royal_wedding":
                    n = RoyalWedding(k) ? 1 : 0;
                    break;
            }
            if (n <= 0) return ActionHandler.Result(r, false, "no_effect", "no effect", null);
            ChatRealmsMod.Announce(r.UserName + ": " + r.Action + " -> " + k.name);
            var d = new JObject();
            d["kingdom"] = k.name;
            return ActionHandler.Result(r, true, "ok", r.Action, d);
        }

        private static bool RoyalBirth(Kingdom k)
        {
            Actor king = k.king;
            City home = k.capital;
            if (king == null || !king.isAlive() || home == null) return false;
            string species = k.getSpecies();
            if (string.IsNullOrEmpty(species)) return false;
            Actor kid = World.world.units.spawnNewUnit(species, GameOps.Around(home.getTile(), 3), false, false, 0f, null, false, false);
            if (kid == null) return false;
            kid.data.parent_id_1 = king.data.id;
            if (king.lover != null) kid.data.parent_id_2 = king.lover.data.id;
            kid.joinCity(home);
            if (king.hasFamily()) kid.setFamily(king.family);
            if (king.hasClan()) kid.setClan(king.clan);
            return true;
        }

        private static bool RoyalWedding(Kingdom k)
        {
            City home = k.capital;
            if (home == null) return false;
            Actor man = null;
            Actor woman = null;
            foreach (Actor a in new List<Actor>(home.units))
            {
                if (a == null || !a.isAlive() || !a.isAdult() || a.hasLover()) continue;
                if (man == null && a.isSexMale()) man = a;
                else if (woman == null && a.isSexFemale()) woman = a;
                if (man != null && woman != null) break;
            }
            if (man == null || woman == null) return false;
            man.becomeLoversWith(woman);
            return true;
        }
    }

    internal static class Alliances
    {
        public static bool Allied(Kingdom a, Kingdom b)
        {
            Alliance al = a.getAlliance();
            return al != null && al.hasKingdom(b);
        }

        private static JObject Info(RelayRequest r, Kingdom k, Kingdom other, string mode)
        {
            var d = new JObject();
            d["target"] = other.name;
            d["kingUnitId"] = other.king != null ? other.king.data.id : 0L;
            d["kingName"] = other.king != null ? other.king.getName() : "";
            string decision = "accept";
            string reason = "";
            Alliance mine = k.getAlliance();
            Alliance theirs = other.getAlliance();
            if (mine != null && mine == theirs)
            {
                decision = "refuse";
                reason = "already_allied";
            }
            else if (World.world.wars.isInWarWith(k, other))
            {
                decision = "refuse";
                reason = "war";
            }
            else if (mode == "join" && theirs == null)
            {
                decision = "refuse";
                reason = "no_alliance";
            }
            else if (mine != null && theirs != null)
            {
                decision = "refuse";
                reason = "both_allied";
            }
            else if (mode == "join" && !theirs.canJoin(k))
            {
                decision = "refuse";
                reason = "opinion";
            }
            else if (!other.isOpinionTowardsKingdomGood(k) || !k.isOpinionTowardsKingdomGood(other))
            {
                decision = "refuse";
                reason = "opinion";
            }
            d["decision"] = decision;
            d["reason"] = reason;
            return ActionHandler.Result(r, true, "ok", "check", d);
        }

        public static JObject Handle(RelayRequest r, Settings s, Kingdom k)
        {
            if (r.Action == "alliance_status")
            {
                Alliance a = k.getAlliance();
                var d = new JObject();
                d["has"] = a != null;
                if (a != null)
                {
                    d["name"] = a.name;
                    var members = new JArray();
                    foreach (Kingdom m in a.kingdoms_hashset) members.Add(m.name);
                    d["members"] = members;
                    d["founder"] = a.data.founder_kingdom_id == k.getID();
                }
                return ActionHandler.Result(r, true, "ok", "status", d);
            }
            if (r.Action == "alliance_leave")
            {
                Alliance a = k.getAlliance();
                if (a == null) return ActionHandler.Result(r, false, "no_alliance", "no alliance", null);
                string name = a.name;
                a.leave(k);
                if (a.kingdoms_hashset.Count < 2 && World.world.alliances.get(a.id) != null) World.world.alliances.dissolveAlliance(a);
                var d = new JObject();
                d["alliance"] = name;
                return ActionHandler.Result(r, true, "ok", "left", d);
            }
            if (r.Action == "alliance_dissolve")
            {
                Alliance a = k.getAlliance();
                if (a == null) return ActionHandler.Result(r, false, "no_alliance", "no alliance", null);
                if (a.data.founder_kingdom_id != k.getID()) return ActionHandler.Result(r, false, "not_founder", "not founder", null);
                string name = a.name;
                World.world.alliances.dissolveAlliance(a);
                var d = new JObject();
                d["alliance"] = name;
                return ActionHandler.Result(r, true, "ok", "dissolved", d);
            }

            Kingdom other = GameOps.FindKingdom((string)r.Args["target"]);
            if (other == null) return ActionHandler.Result(r, false, "target_not_found", "target not found", null);
            if (other == k) return ActionHandler.Result(r, false, "invalid_target", "cannot target self", null);

            if (r.Action == "alliance_check") return Info(r, k, other, (string)r.Args["mode"] ?? "form");

            if (r.Action == "alliance_form" || r.Action == "alliance_join")
            {
                string mode = r.Action == "alliance_join" ? "join" : "form";
                JObject check = Info(r, k, other, mode);
                bool approved = (bool?)r.Args["approved"] == true;
                string why = (string)check["data"]["reason"];
                if ((string)check["data"]["decision"] != "accept" && (!approved || why != "opinion"))
                {
                    return ActionHandler.Result(r, false, "alliance_refused", why, check["data"] as JObject);
                }
                if (World.world.wars.isInWarWith(k, other)) return ActionHandler.Result(r, false, "alliance_refused", "war", null);
                Alliance mine = k.getAlliance();
                Alliance theirs = other.getAlliance();
                if (mine != null && theirs != null) return ActionHandler.Result(r, false, "alliance_refused", "both_allied", null);
                Alliance result;
                if (mine == null && theirs == null)
                {
                    result = World.world.alliances.newAlliance(k, other);
                }
                else if (theirs != null)
                {
                    theirs.join(k, true, true);
                    result = theirs;
                }
                else
                {
                    mine.join(other, true, true);
                    result = mine;
                }
                var d = new JObject();
                d["target"] = other.name;
                d["alliance"] = result != null ? result.name : "";
                ChatRealmsMod.Announce(k.name + " + " + other.name);
                return ActionHandler.Result(r, true, "ok", "allied", d);
            }

            return ActionHandler.Result(r, false, "unknown_action", "unknown action", null);
        }
    }
}
