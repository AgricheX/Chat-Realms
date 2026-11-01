using System;
using System.Collections.Generic;

namespace ChatRealms
{
    internal class EventDef
    {
        public string Id;
        public string En;
        public string Pt;
        public bool Destructive;
        public bool KingdomScope;
        public bool ChaosOnly;
        public Func<EventTarget, bool> Run;

        public string Name(bool pt)
        {
            return pt ? Pt : En;
        }
    }

    internal static class Events
    {
        private static List<EventDef> _all;

        public static List<EventDef> All
        {
            get
            {
                if (_all == null) _all = Build();
                return _all;
            }
        }

        private static void Add(List<EventDef> l, string id, string en, string pt, bool destructive, bool kingdom, Func<EventTarget, bool> run)
        {
            l.Add(new EventDef { Id = id, En = en, Pt = pt, Destructive = destructive, KingdomScope = kingdom, Run = run });
        }

        private static void Disaster(List<EventDef> l, string id, string en, string pt, bool destructive)
        {
            Add(l, "disaster_" + id, en, pt, destructive, false, t => GameOps.Disaster(id));
        }

        private static void Age(List<EventDef> l, string id, string en, string pt, bool destructive)
        {
            Add(l, id, en, pt, destructive, false, t => GameOps.SetAge(id));
        }

        private static void DropEvent(List<EventDef> l, string id, string drop, string en, string pt, bool destructive, int per, int spread, int anchors)
        {
            Add(l, id, en, pt, destructive, true, t => GameOps.DropOn(t, drop, per, spread, anchors));
        }

        private static List<EventDef> Build()
        {
            var l = new List<EventDef>();

            Disaster(l, "tornado", "Tornado", "Tornado", true);
            Disaster(l, "heatwave", "Heatwave", "Onda de calor", true);
            Disaster(l, "small_meteorite", "Meteorite", "Meteorito", true);
            Disaster(l, "small_earthquake", "Earthquake", "Terremoto", true);
            Disaster(l, "hellspawn", "Hellspawn", "Cria do inferno", true);
            Disaster(l, "ice_ones_awoken", "Ice Ones awaken", "Gigantes de gelo despertam", true);
            Disaster(l, "sudden_snowman", "Sudden snowman", "Boneco de neve repentino", false);
            Disaster(l, "garden_surprise", "Garden surprise", "Surpresa no jardim", false);
            Disaster(l, "dragon_from_farlands", "Dragon from the farlands", "Dragao das terras distantes", true);
            Disaster(l, "ash_bandits", "Ash bandits", "Bandidos das cinzas", true);
            Disaster(l, "alien_invasion", "Alien invasion", "Invasao alienigena", true);
            Disaster(l, "biomass", "Biomass outbreak", "Surto de biomassa", true);
            Disaster(l, "tumor", "Tumor outbreak", "Surto de tumor", true);
            Disaster(l, "wild_mage", "Wild mage", "Mago selvagem", true);
            Disaster(l, "underground_necromancer", "Underground necromancer", "Necromante subterraneo", true);
            Disaster(l, "mad_thoughts", "Mad thoughts", "Pensamentos insanos", true);
            Disaster(l, "greg_abominations", "Greg abominations", "Abominacoes Greg", true);

            Age(l, "age_hope", "Age of Hope", "Era da Esperanca", false);
            Age(l, "age_sun", "Age of Sun", "Era do Sol", false);
            Age(l, "age_moon", "Age of Moon", "Era da Lua", false);
            Age(l, "age_wonders", "Age of Wonders", "Era das Maravilhas", false);
            Age(l, "age_dark", "Age of Dark", "Era das Trevas", true);
            Age(l, "age_tears", "Age of Tears", "Era das Lagrimas", true);
            Age(l, "age_chaos", "Age of Chaos", "Era do Caos", true);
            Age(l, "age_ice", "Age of Ice", "Era do Gelo", true);
            Age(l, "age_ash", "Age of Ash", "Era das Cinzas", true);
            Age(l, "age_despair", "Age of Despair", "Era do Desespero", true);

            DropEvent(l, "k_bless", "blessing", "Blessing", "Bencao", false, 4, 5, 3);
            DropEvent(l, "k_inspire", "inspiration", "Inspiration", "Inspiracao", false, 4, 5, 3);
            DropEvent(l, "k_cure", "cure", "Healing rain", "Chuva de cura", false, 4, 5, 3);
            DropEvent(l, "k_shield", "shield", "Divine shield", "Escudo divino", false, 4, 5, 3);
            DropEvent(l, "k_friendship", "friendship", "Friendship", "Amizade", false, 4, 5, 3);
            DropEvent(l, "k_rain", "rain", "Rain", "Chuva", false, 6, 6, 3);
            DropEvent(l, "k_grow", "life_seed", "Life seeds", "Sementes da vida", false, 5, 6, 3);
            DropEvent(l, "k_party", "fireworks", "Fireworks festival", "Festival de fogos", false, 6, 5, 3);
            DropEvent(l, "k_curse", "curse", "Curse", "Maldicao", true, 4, 5, 3);
            DropEvent(l, "k_plague", "plague", "Plague", "Peste", true, 3, 4, 3);
            DropEvent(l, "k_madness", "madness", "Madness", "Loucura", true, 3, 4, 3);
            DropEvent(l, "k_fire", "fire", "Fire", "Incendio", true, 5, 5, 3);
            DropEvent(l, "k_discord", "discord", "Discord", "Discordia", true, 4, 5, 3);
            DropEvent(l, "k_spite", "spite", "Spite", "Rancor", true, 4, 5, 3);
            DropEvent(l, "k_bomb", "bomb", "Bombing", "Bombardeio", true, 3, 5, 3);
            l.Find(e => e.Id == "k_bomb").ChaosOnly = true;
            DropEvent(l, "k_acid", "acid", "Acid rain", "Chuva acida", true, 5, 5, 3);

            Add(l, "k_gold", "Gold gift", "Presente de ouro", false, true, t => GameOps.Gift(t, "gold", 60) > 0);
            Add(l, "k_wheat", "Harvest gift", "Presente de colheita", false, true, t => GameOps.Gift(t, "wheat", 80) > 0);
            Add(l, "k_reinforce", "Reinforcements", "Reforcos", false, true, t => GameOps.Reinforce(t, 6) > 0);
            Add(l, "k_war", "War declaration", "Declaracao de guerra", true, true, t =>
            {
                Kingdom other = GameOps.PickWarTarget(t.K);
                return GameOps.DeclareWar(t.K, other);
            });
            Add(l, "k_peace", "Peace treaty", "Tratado de paz", false, true, t => GameOps.MakePeace(t.K) > 0);

            return l;
        }
    }
}
