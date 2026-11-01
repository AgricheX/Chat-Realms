using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChatRealms
{
    internal static class Items
    {
        private class Gather
        {
            public int Level;
            public int Amount;
            public float Chance;
            public int Hurt;
        }

        private static readonly Dictionary<string, Gather> Defs = new Dictionary<string, Gather>
        {
            { "wood", new Gather { Level = 1, Amount = 12, Chance = 1f, Hurt = 0 } },
            { "stone", new Gather { Level = 1, Amount = 12, Chance = 1f, Hurt = 0 } },
            { "bones", new Gather { Level = 1, Amount = 8, Chance = 0.9f, Hurt = 0 } },
            { "leather", new Gather { Level = 2, Amount = 6, Chance = 0.85f, Hurt = 5 } },
            { "common_metals", new Gather { Level = 2, Amount = 7, Chance = 0.9f, Hurt = 4 } },
            { "gems", new Gather { Level = 4, Amount = 4, Chance = 0.75f, Hurt = 5 } },
            { "gold", new Gather { Level = 4, Amount = 6, Chance = 0.8f, Hurt = 4 } },
            { "silver", new Gather { Level = 5, Amount = 4, Chance = 0.7f, Hurt = 6 } },
            { "mythril", new Gather { Level = 8, Amount = 2, Chance = 0.55f, Hurt = 12 } },
            { "adamantine", new Gather { Level = 12, Amount = 1, Chance = 0.4f, Hurt = 18 } },
            { "dragon_scales", new Gather { Level = 16, Amount = 1, Chance = 0.25f, Hurt = 30 } }
        };

        private static readonly Dictionary<string, EquipmentType> Slots = new Dictionary<string, EquipmentType>
        {
            { "weapon", EquipmentType.Weapon }, { "helmet", EquipmentType.Helmet }, { "armor", EquipmentType.Armor },
            { "boots", EquipmentType.Boots }, { "ring", EquipmentType.Ring }, { "amulet", EquipmentType.Amulet }
        };

        private static JObject Fail(ActCtx c, string code, JObject data = null)
        {
            return ActionHandler.Result(c.R, false, code, code, data);
        }

        private static EquipmentAsset Asset(string id)
        {
            if (string.IsNullOrEmpty(id) || id.StartsWith("$") || id.StartsWith("boat_")) return null;
            EquipmentAsset ea = AssetManager.items.get(id);
            if (ea == null || ea.equipment_value <= 0) return null;
            return ea;
        }

        private static string SlotName(EquipmentType t)
        {
            foreach (KeyValuePair<string, EquipmentType> p in Slots)
            {
                if (p.Value == t) return p.Key;
            }
            return "weapon";
        }

        private static string ItemName(Item i)
        {
            try
            {
                return i.getName();
            }
            catch (Exception)
            {
                return i.getAsset().id;
            }
        }

        private static Item Make(EquipmentAsset ea, Actor owner)
        {
            return World.world.items.generateItem(ea, owner.kingdom != null && owner.kingdom.isCiv() ? owner.kingdom : null, owner.getName(), 2, owner, 0, true);
        }

        public static void Register(Dictionary<string, UnitAct> d)
        {
            UnitActions.Add(d, "unit_gather", 1, false, c =>
            {
                string res = (string)c.R.Args["resource"];
                Gather g;
                if (string.IsNullOrEmpty(res) || !Defs.TryGetValue(res, out g)) return Fail(c, "unknown_resource");
                int level = c.U.data.level;
                if (level < g.Level)
                {
                    var need = new JObject();
                    need["level"] = g.Level;
                    need["resource"] = res;
                    return Fail(c, "level_required", need);
                }
                var d2 = ActionHandler.UnitInfo(c.U);
                d2["resource"] = res;
                bool found = UnityEngine.Random.value <= g.Chance;
                int hurt = 0;
                if (g.Hurt > 0 && UnityEngine.Random.value < 0.5f)
                {
                    hurt = g.Hurt;
                    int max = c.U.getMaxHealth();
                    int loss = Mathf.Max(1, max * g.Hurt / 100);
                    c.U.data.health = Mathf.Max(Mathf.Max(1, max / 10), c.U.data.health - loss);
                }
                int amount = 0;
                if (found)
                {
                    float mult = 1f + Mathf.Min(level, 40) * 0.06f;
                    amount = Mathf.Max(1, Mathf.RoundToInt(g.Amount * mult * UnityEngine.Random.Range(0.7f, 1.3f)));
                }
                c.U.addExperience(1);
                d2["amount"] = amount;
                d2["hurt"] = hurt;
                return ActionHandler.Result(c.R, true, "ok", "gathered", d2);
            });

            UnitActions.Add(d, "unit_equip", 1, false, c =>
            {
                EquipmentAsset ea = Asset((string)c.R.Args["asset"]);
                if (ea == null) return Fail(c, "unknown_item");
                if (!c.U.canUseItems()) return Fail(c, "cannot_use_items");
                ActorEquipmentSlot slot = c.U.equipment.getSlot(ea.equipment_type);
                if (!slot.canChangeSlot()) return Fail(c, "item_cursed");
                string replaced = slot.isEmpty() ? "" : ItemName(slot.getItem());
                Item item = Make(ea, c.U);
                c.U.equipment.setItem(item, c.U);
                c.U.setStatsDirty();
                var d2 = ActionHandler.UnitInfo(c.U);
                d2["asset"] = ea.id;
                d2["slot"] = SlotName(ea.equipment_type);
                d2["item"] = ItemName(item);
                d2["replaced"] = replaced;
                int filled = 0;
                foreach (ActorEquipmentSlot sl in c.U.equipment)
                {
                    if (!sl.isEmpty()) filled++;
                }
                d2["slots"] = filled;
                ChatRealmsMod.Announce(c.U.getName() + " + " + ItemName(item));
                return ActionHandler.Result(c.R, true, "ok", "equipped", d2);
            });

            UnitActions.Add(d, "unit_equip_troops", 3, false, c =>
            {
                EquipmentAsset ea = Asset((string)c.R.Args["asset"]);
                if (ea == null) return Fail(c, "unknown_item");
                City city = c.U.city;
                if (city == null) return Fail(c, "no_city");
                int want = Mathf.Clamp(c.R.Args["count"] != null ? (int)c.R.Args["count"] : 1, 1, 10);
                int done = 0;
                foreach (Actor a in new List<Actor>(city.units))
                {
                    if (done >= want) break;
                    if (a == null || !a.isAlive() || a == c.U || !a.isWarrior() || !a.canUseItems()) continue;
                    ActorEquipmentSlot slot = a.equipment.getSlot(ea.equipment_type);
                    if (!slot.canChangeSlot()) continue;
                    if (!slot.isEmpty() && slot.getItem().getAsset().equipment_value >= ea.equipment_value) continue;
                    a.equipment.setItem(Make(ea, a), a);
                    a.setStatsDirty();
                    done++;
                }
                if (done == 0) return Fail(c, "no_effect");
                var d2 = ActionHandler.UnitInfo(c.U);
                d2["asset"] = ea.id;
                d2["equipped"] = done;
                ChatRealmsMod.Announce(city.name + " +" + done);
                return ActionHandler.Result(c.R, true, "ok", "troops equipped", d2);
            });

            UnitActions.Add(d, "unit_arsenal", 3, false, c =>
            {
                City city = c.U.city;
                if (city == null || c.K == null || city.kingdom != c.K) return Fail(c, "not_in_city");
                string slotName = (string)c.R.Args["slot"] ?? "list";
                if (city.data.equipment == null) return Fail(c, "arsenal_empty");
                var d2 = ActionHandler.UnitInfo(c.U);
                if (slotName == "list")
                {
                    var counts = new JObject();
                    foreach (KeyValuePair<string, EquipmentType> p in Slots)
                    {
                        counts[p.Key] = city.data.equipment.getEquipmentList(p.Value).Count;
                    }
                    d2["arsenal"] = counts;
                    return ActionHandler.Result(c.R, true, "ok", "arsenal", d2);
                }
                EquipmentType type;
                if (!Slots.TryGetValue(slotName, out type)) return Fail(c, "unknown_item");
                List<long> list = city.data.equipment.getEquipmentList(type);
                Item best = null;
                foreach (long id in list)
                {
                    Item it = World.world.items.get(id);
                    if (it == null || it.getAsset() == null) continue;
                    if (best == null || it.getAsset().equipment_value > best.getAsset().equipment_value) best = it;
                }
                if (best == null) return Fail(c, "arsenal_empty");
                ActorEquipmentSlot slot = c.U.equipment.getSlot(type);
                if (!slot.canChangeSlot()) return Fail(c, "item_cursed");
                if (!c.U.canUseItems()) return Fail(c, "cannot_use_items");
                list.Remove(best.id);
                best.clearCity();
                c.U.equipment.setItem(best, c.U);
                c.U.setStatsDirty();
                Cohesion.Add(c.K, -1f);
                d2["item"] = ItemName(best);
                d2["slot"] = slotName;
                return ActionHandler.Result(c.R, true, "ok", "arsenal taken", d2);
            });

            UnitActions.Add(d, "unit_arsenal_put", 4, false, c =>
            {
                EquipmentAsset ea = Asset((string)c.R.Args["asset"]);
                if (ea == null) return Fail(c, "unknown_item");
                City city = c.U.city;
                if (city == null || c.K == null || city.kingdom != c.K) return Fail(c, "not_in_city");
                Item item = Make(ea, c.U);
                city.tryToPutItem(item);
                if (item.getCity() != city)
                {
                    try
                    {
                        World.world.items.removeObject(item);
                    }
                    catch (Exception)
                    {
                    }
                    return Fail(c, "arsenal_full");
                }
                Cohesion.Add(c.K, 1f);
                var d2 = ActionHandler.UnitInfo(c.U);
                d2["asset"] = ea.id;
                d2["item"] = ItemName(item);
                return ActionHandler.Result(c.R, true, "ok", "arsenal stocked", d2);
            });
        }
    }
}
