using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace ChatRealms
{
    internal static class WorldInfo
    {
        private const string Key = "chatrealms_world";
        private const string RulesKey = "chatrealms_rules";
        private static string _applied = "";

        private static readonly string[] Off =
        {
            "world_law_terramorphing", "world_law_diplomacy", "world_law_rebellions",
            "world_law_evolution_events", "world_law_disasters_other", "world_law_rites"
        };

        private static readonly string[] On = { "world_law_gaias_covenant" };

        public static string Current()
        {
            try
            {
                if (World.world == null || World.world.map_stats == null) return "";
                MapStats ms = World.world.map_stats;
                if (ms.custom_data == null) ms.custom_data = new SaveCustomData();
                string id;
                ms.custom_data.get(Key, out id, "");
                if (string.IsNullOrEmpty(id))
                {
                    id = Fingerprint(ms) ?? Guid.NewGuid().ToString("N");
                    ms.custom_data.set(Key, id);
                }
                return id;
            }
            catch (Exception)
            {
                return "";
            }
        }

        private static string Fingerprint(MapStats ms)
        {
            try
            {
                var kingdoms = GameOps.CivKingdoms();
                var cities = new List<City>();
                if (World.world.cities != null)
                {
                    foreach (City c in World.world.cities.list)
                    {
                        if (c != null) cities.Add(c);
                    }
                }
                if (kingdoms.Count == 0 && cities.Count == 0) return null;
                kingdoms.Sort((x, y) => x.getID().CompareTo(y.getID()));
                cities.Sort((x, y) => x.getID().CompareTo(y.getID()));
                var sb = new StringBuilder();
                sb.Append(MapBox.width).Append('x').Append(MapBox.height).Append('|').Append(ms.name).Append('|');
                sb.Append(ms.id_kingdom).Append('|').Append(ms.id_city).Append('|');
                for (int i = 0; i < kingdoms.Count && i < 12; i++) sb.Append(kingdoms[i].getID()).Append(':').Append(kingdoms[i].name).Append(';');
                sb.Append('|');
                for (int i = 0; i < cities.Count && i < 12; i++) sb.Append(cities[i].getID()).Append(':').Append(cities[i].name).Append(';');
                using (var sha = SHA1.Create())
                {
                    byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
                    var hex = new StringBuilder("s");
                    for (int i = 0; i < 16; i++) hex.Append(hash[i].ToString("x2"));
                    return hex.ToString();
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static void ApplyRules(Settings s)
        {
            string id = Current();
            if (id.Length == 0 || id == _applied) return;
            _applied = id;
            if (!s.ApplyRules) return;
            try
            {
                SaveCustomData data = World.world.map_stats.custom_data;
                string done;
                data.get(RulesKey, out done, "");
                if (done == "1") return;
                data.set(RulesKey, "1");
                var laws = World.world.world_laws;
                int changed = 0;
                foreach (string law in Off)
                {
                    if (laws.dict.ContainsKey(law) && laws.dict[law].boolVal)
                    {
                        laws.dict[law].boolVal = false;
                        changed++;
                    }
                }
                foreach (string law in On)
                {
                    if (laws.dict.ContainsKey(law) && !laws.dict[law].boolVal)
                    {
                        laws.dict[law].boolVal = true;
                        changed++;
                    }
                }
                laws.updateCaches();
                if (changed > 0)
                {
                    ChatRealmsMod.Announce(s.Portuguese
                        ? "ChatRealms aplicou as regras de RP neste mundo (" + changed + " leis mudaram). Você pode mudar de volta nas leis do mundo."
                        : "ChatRealms applied the RP rules to this world (" + changed + " laws changed). You can change them back in the world laws.");
                }
            }
            catch (Exception e)
            {
                ChatRealmsMod.LogWarning("rules: " + e.Message);
            }
        }
    }
}
