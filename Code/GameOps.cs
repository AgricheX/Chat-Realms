using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChatRealms
{
    internal class EventTarget
    {
        public Kingdom K;
        public City C;

        public string Label
        {
            get
            {
                if (C != null) return C.name;
                if (K != null) return K.name;
                return "";
            }
        }

        public List<WorldTile> Anchors(int max)
        {
            var tiles = new List<WorldTile>();
            if (C != null)
            {
                WorldTile t = C.getTile();
                if (t != null) tiles.Add(t);
                return tiles;
            }
            if (K == null) return tiles;
            if (K.capital != null)
            {
                WorldTile t = K.capital.getTile();
                if (t != null) tiles.Add(t);
            }
            var cities = new List<City>(K.cities);
            while (cities.Count > 0 && tiles.Count < max)
            {
                int i = UnityEngine.Random.Range(0, cities.Count);
                City c = cities[i];
                cities.RemoveAt(i);
                if (c == K.capital) continue;
                WorldTile t = c.getTile();
                if (t != null) tiles.Add(t);
            }
            return tiles;
        }
    }

    internal static class GameOps
    {
        public static bool Loaded
        {
            get { return World.world != null && Config.game_loaded && World.world.tiles_list != null && World.world.tiles_list.Length > 0 && !SmoothLoader.isLoading(); }
        }

        public static bool WorldReady
        {
            get { return Loaded && !Config.paused; }
        }

        public static List<Kingdom> CivKingdoms()
        {
            var list = new List<Kingdom>();
            if (World.world == null || World.world.kingdoms == null) return list;
            foreach (Kingdom k in World.world.kingdoms.list)
            {
                if (k != null && k.isCiv() && k.cities.Count > 0) list.Add(k);
            }
            return list;
        }

        public static Kingdom FindKingdom(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (Kingdom k in CivKingdoms())
            {
                if (string.Equals(k.name, name.Trim(), StringComparison.OrdinalIgnoreCase)) return k;
            }
            return null;
        }

        public static City FindCity(string name)
        {
            if (string.IsNullOrEmpty(name) || World.world == null || World.world.cities == null) return null;
            foreach (City c in World.world.cities.list)
            {
                if (c != null && string.Equals(c.name, name.Trim(), StringComparison.OrdinalIgnoreCase)) return c;
            }
            return null;
        }

        public static Kingdom Largest()
        {
            Kingdom best = null;
            int pop = -1;
            foreach (Kingdom k in CivKingdoms())
            {
                int p = k.getPopulationPeople();
                if (p > pop)
                {
                    pop = p;
                    best = k;
                }
            }
            return best;
        }

        public static Kingdom RandomKingdom()
        {
            var l = CivKingdoms();
            if (l.Count == 0) return null;
            return l[UnityEngine.Random.Range(0, l.Count)];
        }

        public static EventTarget ResolveRoleplay(string name)
        {
            var t = new EventTarget();
            City c = FindCity(name);
            if (c != null)
            {
                t.C = c;
                t.K = c.kingdom;
                return t;
            }
            Kingdom k = FindKingdom(name);
            if (k == null) k = Largest();
            t.K = k;
            return t;
        }

        public static WorldTile Around(WorldTile center, int radius)
        {
            if (center == null || World.world == null) return center;
            int x = Mathf.Clamp(center.x + UnityEngine.Random.Range(-radius, radius + 1), 0, MapBox.width - 1);
            int y = Mathf.Clamp(center.y + UnityEngine.Random.Range(-radius, radius + 1), 0, MapBox.height - 1);
            WorldTile t = World.world.GetTile(x, y);
            return t ?? center;
        }

        public static bool Drop(WorldTile tile, string dropId, int count, int spread)
        {
            if (tile == null || World.world == null || World.world.drop_manager == null) return false;
            bool any = false;
            for (int i = 0; i < count; i++)
            {
                WorldTile t = spread > 0 ? Around(tile, spread) : tile;
                try
                {
                    World.world.drop_manager.spawn(t, dropId);
                    any = true;
                }
                catch (Exception e)
                {
                    ChatRealmsMod.LogWarning("drop " + dropId + ": " + e.Message);
                }
            }
            return any;
        }

        public static bool DropOn(EventTarget target, string dropId, int perAnchor, int spread, int maxAnchors)
        {
            bool any = false;
            foreach (WorldTile t in target.Anchors(maxAnchors))
            {
                if (Drop(t, dropId, perAnchor, spread)) any = true;
            }
            return any;
        }

        public static bool Disaster(string id)
        {
            if (AssetManager.disasters == null) return false;
            DisasterAsset asset = AssetManager.disasters.get(id);
            if (asset == null || asset.action == null) return false;
            asset.action(asset);
            return true;
        }

        public static bool SetAge(string id)
        {
            WorldAgeAsset asset = AssetManager.era_library.get(id);
            if (asset == null) return false;
            World.world.era_manager.setCurrentAge(asset, true);
            return true;
        }

        public static int Gift(EventTarget target, string resource, int amount)
        {
            int total = 0;
            var cities = new List<City>();
            if (target.C != null) cities.Add(target.C);
            else if (target.K != null) cities.AddRange(target.K.cities);
            foreach (City c in cities)
            {
                if (c == null) continue;
                total += c.addResourcesToRandomStockpile(resource, amount);
            }
            return total;
        }

        public static int Reinforce(EventTarget target, int count)
        {
            if (target.K == null) return 0;
            string species = target.K.getSpecies();
            if (string.IsNullOrEmpty(species)) return 0;
            int made = 0;
            List<WorldTile> anchors = target.Anchors(3);
            City home = target.C ?? target.K.capital;
            foreach (WorldTile a in anchors)
            {
                for (int i = 0; i < count; i++)
                {
                    try
                    {
                        Actor u = World.world.units.spawnNewUnit(species, Around(a, 3), false, false, 0f, null, false, true);
                        if (u == null) continue;
                        if (home != null)
                        {
                            u.joinCity(home);
                            if (!u.isWarrior()) home.makeWarrior(u);
                        }
                        made++;
                    }
                    catch (Exception e)
                    {
                        ChatRealmsMod.LogWarning("reinforce: " + e.Message);
                    }
                }
                break;
            }
            if (home != null && made > 0)
            {
                try
                {
                    home.checkArmyExistence();
                }
                catch (Exception)
                {
                }
            }
            return made;
        }

        public static Actor FindHeir(long deadId, long familyId, long clanId, HashSet<long> exclude, long preferred)
        {
            if (preferred != 0)
            {
                Actor named = World.world.units.get(preferred);
                if (named != null && named.isAlive() && !exclude.Contains(named.data.id)) return named;
            }
            var pool = new List<Actor>();
            var seen = new HashSet<long>();
            Family fam = familyId != 0 ? World.world.families.get(familyId) : null;
            Clan clan = clanId != 0 ? World.world.clans.get(clanId) : null;
            var famSet = new HashSet<long>();
            if (fam != null)
            {
                foreach (Actor a in fam.units)
                {
                    if (a != null) famSet.Add(a.data.id);
                    pool.Add(a);
                }
            }
            if (clan != null)
            {
                foreach (Actor a in clan.units) pool.Add(a);
            }
            for (int pass = 0; pass < 2; pass++)
            {
                for (int tier = 1; tier <= 5; tier++)
                {
                    Actor best = null;
                    float bestAge = -1f;
                    foreach (Actor a in pool)
                    {
                        if (a == null || !a.isAlive() || a.data.id == deadId || exclude.Contains(a.data.id)) continue;
                        if (pass == 0 && (a.kingdom == null || !a.kingdom.isCiv())) continue;
                        bool child = a.isChildOf(deadId);
                        bool adult = a.isAdult();
                        bool inFamily = famSet.Contains(a.data.id);
                        bool ok;
                        switch (tier)
                        {
                            case 1: ok = child && adult; break;
                            case 2: ok = child && !adult; break;
                            case 3: ok = inFamily && adult; break;
                            case 4: ok = adult; break;
                            default: ok = true; break;
                        }
                        if (!ok) continue;
                        float age = a.getAge();
                        if (age > bestAge)
                        {
                            bestAge = age;
                            best = a;
                        }
                    }
                    if (best != null) return best;
                }
            }
            return null;
        }

        public static Kingdom PickWarTarget(Kingdom k)
        {
            var options = new List<Kingdom>();
            foreach (Kingdom o in CivKingdoms())
            {
                if (o == k) continue;
                if (World.world.wars.isInWarWith(k, o)) continue;
                options.Add(o);
            }
            if (options.Count == 0) return null;
            return options[UnityEngine.Random.Range(0, options.Count)];
        }

        public static bool DeclareWar(Kingdom a, Kingdom b)
        {
            if (a == null || b == null || a == b) return false;
            if (World.world.wars.isInWarWith(a, b)) return false;
            War w = World.world.wars.newWar(a, b, WarTypeLibrary.normal);
            return w != null;
        }

        public static int MakePeace(Kingdom k)
        {
            var wars = new List<War>();
            foreach (War w in World.world.wars.getWars(k)) wars.Add(w);
            foreach (War w in wars) World.world.wars.endWar(w);
            return wars.Count;
        }
    }
}
