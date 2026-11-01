using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace ChatRealms
{
    internal static class Intel
    {
        public static bool Handles(string action)
        {
            return action == "info_unit" || action == "info_kingdom" || action == "info_city" ||
                   action == "info_wars" || action == "world_kingdoms";
        }

        private static string SizeOf(int pop)
        {
            if (pop < 25) return "tiny";
            if (pop < 80) return "small";
            if (pop < 200) return "medium";
            if (pop < 500) return "large";
            return "vast";
        }

        private static string Ruler(Kingdom k)
        {
            return k.king != null ? k.king.getName() : "";
        }

        private static string WarName(War w)
        {
            try
            {
                var a = w.getAsset();
                if (a == null || string.IsNullOrEmpty(a.localized_type) || !LocalizedTextManager.stringExists(a.localized_type)) return "";
                return LocalizedTextManager.getText(a.localized_type);
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string Side(IEnumerable<Kingdom> list)
        {
            var names = new List<string>();
            foreach (Kingdom k in list) names.Add(k.name);
            return string.Join(", ", names);
        }

        private static JObject WarDetail(War w)
        {
            var o = new JObject();
            o["type"] = WarName(w);
            o["years"] = w.getDuration();
            o["attackers"] = Side(w.getAttackers());
            o["defenders"] = Side(w.getDefenders());
            o["deadAttackers"] = w.data.dead_attackers;
            o["deadDefenders"] = w.data.dead_defenders;
            o["attackerCities"] = w.countAttackersCities();
            o["defenderCities"] = w.countDefendersCities();
            o["attackerWarriors"] = w.countAttackersWarriors();
            o["defenderWarriors"] = w.countDefendersWarriors();
            return o;
        }

        private static JObject WarBasic(War w)
        {
            var o = new JObject();
            o["attackers"] = Side(w.getAttackers());
            o["defenders"] = Side(w.getDefenders());
            return o;
        }

        public static JObject Handle(RelayRequest r, Settings s, Actor unit)
        {
            int rank = 5;
            Kingdom home = GameOps.FindKingdom(r.Kingdom);
            if (s.UnitMode)
            {
                if (unit == null) return ActionHandler.Result(r, false, r.UnitId != 0 ? "unit_dead" : "unit_required", "unit required", null);
                rank = UnitActions.Rank(unit);
                home = unit.kingdom != null && unit.kingdom.isCiv() ? unit.kingdom : null;
            }
            if (home == null && unit == null) return ActionHandler.Result(r, false, "kingdom_gone", "kingdom not found", null);

            switch (r.Action)
            {
                case "info_unit": return UnitView(r, unit, rank);
                case "info_kingdom": return KingdomView(r, home, rank);
                case "info_city": return CityView(r, home, unit, rank);
                case "info_wars": return WarsView(r, s, home, rank);
                default: return WorldView(r, home, rank);
            }
        }

        private static float Stat(Actor u, string key)
        {
            try { return u.stats[key]; } catch (Exception) { return 0f; }
        }

        private static string NameOf(Actor a)
        {
            return a != null ? a.getName() : "";
        }

        private static JObject UnitView(RelayRequest r, Actor u, int rank)
        {
            if (u == null) return ActionHandler.Result(r, false, "unit_required", "unit required", null);
            JObject d = ActionHandler.UnitInfo(u);
            d["traits"] = u.getTraitsAsLocalizedString();
            d["level"] = u.data.level;
            d["kills"] = u.data.kills;
            d["happiness"] = u.getHappinessPercent();
            d["rank"] = rank;
            try
            {
                d["profession"] = u.getProfession().ToString().ToLowerInvariant();
            }
            catch (Exception)
            {
                d["profession"] = "";
            }
            var st = new JObject();
            foreach (string k in new[] { "damage", "armor", "speed", "attack_speed", "range", "critical_chance", "accuracy", "stamina", "intelligence", "warfare", "diplomacy", "stewardship", "lifespan" })
            {
                st[k] = Math.Round(Stat(u, k), 1);
            }
            st["health"] = u.getHealth();
            st["max_health"] = u.getMaxHealth();
            d["stats"] = st;
            try { d["hunger"] = Math.Round(u.getNutritionRatio() * 100f); } catch (Exception) { }
            var eq = new JObject();
            try
            {
                foreach (var pair in u.equipment._dictionary)
                {
                    eq[pair.Key.ToString().ToLowerInvariant()] = pair.Value.isEmpty() ? "" : pair.Value.getItem().getName();
                }
            }
            catch (Exception)
            {
            }
            d["equipment"] = eq;
            try { d["lover"] = u.hasLover() ? NameOf(u.lover) : ""; } catch (Exception) { }
            try { d["bestFriend"] = u.hasBestFriend() ? NameOf(u.getBestFriend()) : ""; } catch (Exception) { }
            var kids = new JArray();
            try
            {
                foreach (Actor c in u.getChildren())
                {
                    if (c != null && c.isAlive() && kids.Count < 8) kids.Add(c.getName());
                }
            }
            catch (Exception)
            {
            }
            d["children"] = kids;
            try { d["culture"] = u.hasCulture() ? u.culture.name : ""; } catch (Exception) { }
            try { d["religion"] = u.hasReligion() ? u.religion.name : ""; } catch (Exception) { }
            try { d["language"] = u.hasLanguage() ? u.language.name : ""; } catch (Exception) { }
            try { d["subspecies"] = u.hasSubspecies() ? u.subspecies.name : ""; } catch (Exception) { }
            var sts = new JArray();
            try
            {
                foreach (string sid in u.getStatusesIds()) sts.Add(sid.Replace('_', ' '));
            }
            catch (Exception)
            {
            }
            d["statuses"] = sts;
            d["food"] = Math.Round(Needs.Food(u), 1);
            d["move"] = Math.Round(Needs.Move(u), 1);
            return ActionHandler.Result(r, true, "ok", "unit", d);
        }

        private static JObject KingdomView(RelayRequest r, Kingdom home, int rank)
        {
            string name = (string)r.Args["name"];
            Kingdom k = string.IsNullOrWhiteSpace(name) ? home : GameOps.FindKingdom(name);
            if (k == null) return ActionHandler.Result(r, false, "kingdom_not_found", "kingdom not found", null);
            bool own = k == home;
            var d = new JObject();
            d["name"] = k.name;
            d["ruler"] = Ruler(k);
            d["own"] = own;
            d["rank"] = rank;
            int pop = k.getPopulationPeople();
            if (own)
            {
                d["size"] = SizeOf(pop);
                d["cities"] = k.cities.Count;
                if (rank >= 4)
                {
                    float coh = Cohesion.Get(k);
                    d["cohesion"] = Math.Round(coh);
                    d["cohesionLabel"] = Cohesion.Label(coh);
                }
                if (rank >= 3)
                {
                    var names = new JArray();
                    foreach (City c in k.cities) names.Add(c.name);
                    d["cityNames"] = names;
                    Alliance al = k.getAlliance();
                    d["alliance"] = al != null ? al.data.name : "";
                }
                if (rank >= 5)
                {
                    d["population"] = pop;
                    d["warriors"] = k.countTotalWarriors();
                    var wars = new JArray();
                    foreach (War w in World.world.wars.getWars(k)) wars.Add(WarBasic(w));
                    d["wars"] = wars;
                }
            }
            else if (rank >= 5)
            {
                d["size"] = SizeOf(pop);
            }
            return ActionHandler.Result(r, true, "ok", "kingdom", d);
        }

        private static JObject CityView(RelayRequest r, Kingdom home, Actor unit, int rank)
        {
            string name = (string)r.Args["name"];
            City c = null;
            if (string.IsNullOrWhiteSpace(name))
            {
                c = unit != null ? unit.city : null;
                if (c == null && home != null && home.capital != null) c = home.capital;
            }
            else
            {
                c = GameOps.FindCity(name);
            }
            if (c == null) return ActionHandler.Result(r, false, "city_not_found", "city not found", null);
            bool own = c.kingdom == home;
            var d = new JObject();
            d["name"] = c.name;
            d["kingdom"] = c.kingdom != null ? c.kingdom.name : "";
            d["own"] = own;
            d["rank"] = rank;
            d["leader"] = c.leader != null ? c.leader.getName() : "";
            d["capital"] = c.isCapitalCity();
            if (own && rank >= 3)
            {
                d["population"] = c.getPopulationPeople();
                d["warriors"] = c.countWarriors();
                d["buildings"] = c.buildings.Count;
                d["gold"] = c.amount_gold;
                d["wood"] = c.amount_wood;
                d["stone"] = c.amount_stone;
            }
            else if (own)
            {
                d["size"] = SizeOf(c.getPopulationPeople());
            }
            return ActionHandler.Result(r, true, "ok", "city", d);
        }

        private static JObject WarsView(RelayRequest r, Settings s, Kingdom home, int rank)
        {
            int rumorYears = s.WarRumorYears;
            var mine = new JArray();
            var rumors = new JArray();
            foreach (War w in World.world.wars)
            {
                if (w == null || w.hasEnded()) continue;
                if (home != null && w.hasKingdom(home))
                {
                    mine.Add(rank >= 3 ? WarDetail(w) : WarBasic(w));
                }
                else if (w.getDuration() >= rumorYears)
                {
                    rumors.Add(WarBasic(w));
                }
            }
            var d = new JObject();
            d["rank"] = rank;
            d["detailed"] = rank >= 3;
            d["mine"] = mine;
            d["rumors"] = rumors;
            d["rumorYears"] = rumorYears;
            return ActionHandler.Result(r, true, "ok", "wars", d);
        }

        private static JObject WorldView(RelayRequest r, Kingdom home, int rank)
        {
            var arr = new JArray();
            foreach (Kingdom k in GameOps.CivKingdoms())
            {
                var o = new JObject();
                o["name"] = k.name;
                o["ruler"] = Ruler(k);
                o["own"] = k == home;
                if (rank >= 5) o["size"] = SizeOf(k.getPopulationPeople());
                arr.Add(o);
            }
            var d = new JObject();
            d["rank"] = rank;
            d["kingdoms"] = arr;
            return ActionHandler.Result(r, true, "ok", "world", d);
        }
    }
}
