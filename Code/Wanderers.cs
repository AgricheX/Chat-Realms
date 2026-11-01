using System.Collections.Generic;

namespace ChatRealms
{
    internal static class Wanderers
    {
        private static readonly HashSet<long> Tracked = new HashSet<long>();
        private static readonly HashSet<long> Guarded = new HashSet<long>();
        private static float _timer;

        private static readonly string[] Ids =
        {
            "check_join_city", "check_join_empty_nearby_city", "check_lover_city",
            "try_to_return_to_home_city", "build_civ_city_here", "try_to_start_new_civilization"
        };

        public static bool Wild(Actor a)
        {
            return a.kingdom == null || !a.kingdom.isCiv();
        }

        public static void Track(long id)
        {
            Tracked.Add(id);
        }

        private static void Apply(Actor a, bool enabled)
        {
            foreach (string id in Ids)
            {
                DecisionAsset d = AssetManager.decisions_library.get(id);
                if (d != null) a.setDecisionState(d.decision_index, enabled);
            }
        }

        public static void Guard(Actor a)
        {
            Tracked.Add(a.data.id);
            Apply(a, false);
            Guarded.Add(a.data.id);
        }

        public static void Release(Actor a)
        {
            Apply(a, true);
            Guarded.Remove(a.data.id);
        }

        public static void Tick(float dt)
        {
            _timer += dt;
            if (_timer < 2f) return;
            _timer = 0f;
            if (Tracked.Count == 0 || World.world == null) return;
            foreach (long id in new List<long>(Tracked))
            {
                Actor a = World.world.units.get(id);
                if (a == null || !a.isAlive())
                {
                    Tracked.Remove(id);
                    Guarded.Remove(id);
                    continue;
                }
                bool wild = Wild(a);
                if (wild && !Guarded.Contains(id)) Guard(a);
                else if (!wild && Guarded.Contains(id)) Release(a);
            }
        }
    }
}
