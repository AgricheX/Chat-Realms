using System;

namespace ChatRealms
{
    internal static class Needs
    {
        private const float Step = 600f;

        private static readonly System.Collections.Generic.HashSet<string> Eating = new System.Collections.Generic.HashSet<string> { "unit_eat" };
        private static readonly System.Collections.Generic.HashSet<string> Feasting = new System.Collections.Generic.HashSet<string> { "unit_party", "unit_sing" };
        private static readonly System.Collections.Generic.HashSet<string> Exercise = new System.Collections.Generic.HashSet<string>
        {
            "unit_combat", "unit_aim", "unit_run", "unit_endure", "unit_fence", "unit_patrol", "unit_work", "unit_mine",
            "unit_hunt", "unit_wood", "unit_march", "unit_explore", "unit_train", "unit_go", "unit_gather"
        };

        public static float Food(Actor a)
        {
            float v;
            a.data.get("cr_food", out v, 0f);
            return v;
        }

        public static float Move(Actor a)
        {
            float v;
            a.data.get("cr_move", out v, 0f);
            return v;
        }

        private static void Decay(Actor a)
        {
            double now = World.world.getCurWorldTime();
            float last;
            a.data.get("cr_time", out last, 0f);
            if (last > 0f)
            {
                float steps = (float)((now - last) / Step);
                if (steps > 0f)
                {
                    a.data.set("cr_food", Math.Max(0f, Food(a) - steps));
                    a.data.set("cr_move", Math.Max(0f, Move(a) - steps * 0.5f));
                }
            }
            a.data.set("cr_time", (float)now);
        }

        public static string OnAction(Actor a, string action)
        {
            try
            {
                Decay(a);
                float food = Food(a);
                float move = Move(a);
                if (Eating.Contains(action)) food += 1f;
                else if (Feasting.Contains(action)) food += 0.5f;
                if (Exercise.Contains(action))
                {
                    move += 1f;
                    food = Math.Max(0f, food - 0.5f);
                }
                a.data.set("cr_food", food);
                a.data.set("cr_move", move);

                bool fat = a.hasTrait("fat");
                bool glutton = a.hasTrait("gluttonous");
                if (!fat && food >= 4f && move < food / 2f)
                {
                    if (a.addTrait("fat", true)) return "fat_gain";
                }
                if (fat && move >= 4f && move > food)
                {
                    a.removeTrait("fat");
                    return "fat_lose";
                }
                if (!glutton && food >= 8f)
                {
                    if (a.addTrait("gluttonous", true)) return "glutton_gain";
                }
                if (glutton && food <= 1f)
                {
                    a.removeTrait("gluttonous");
                    return "glutton_lose";
                }
            }
            catch (Exception)
            {
            }
            return null;
        }
    }
}
