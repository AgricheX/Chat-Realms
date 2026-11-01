using System;
using NeoModLoader.General;
using UnityEngine;
using UnityEngine.UI;

namespace ChatRealms
{
    internal static class CinemaTab
    {
        private const string Id = "chatrealms_cinema";
        private static PowerButton _btn;
        private static Sprite _icon;
        private static float _timer;
        private static int _fails;

        internal static void Tick(float dt, bool pt)
        {
            if (_btn != null)
            {
                if (_btn.icon != null) _btn.icon.color = Cinema.Active ? new Color(1f, 0.82f, 0.4f, 1f) : Color.white;
                return;
            }
            _timer -= dt;
            if (_timer > 0f) return;
            _timer = _fails > 5 ? 5f : 1f;
            try
            {
                PowersTab tab = PowerButtonCreator.GetTab(PowerTabNames.Other);
                if (tab == null) return;
                LM.AddToCurrentLocale(Id, pt ? "Câmera cinema" : "Cinema camera");
                LM.AddToCurrentLocale(Id + " Description", pt ? "Liga a câmera automática que passeia pelo mundo e esconde a interface. Para sair, use F7 ou o botão de mostrar a interface." : "Turns on the automatic camera that roams the world and hides the interface. To leave, press F7 or the show interface button.");
                PowerButton b = PowerButtonCreator.CreateSimpleButton(Id, OnClick, Icon(), tab.transform, Vector2.zero);
                PowerButtonCreator.AddButtonToTab(b, tab, (int?)null);
                _btn = b;
                _fails = 0;
            }
            catch (Exception e)
            {
                if (_fails++ == 0) ChatRealmsMod.LogWarning("cinema tab: " + e.Message);
            }
        }

        private static void OnClick()
        {
            if (!GameOps.WorldReady) return;
            Cinema.Toggle();
            if (Cinema.Active) ChatRealmsMod.Announce(ChatRealmsMod.Instance.Cfg.Portuguese ? "Câmera cinema ligada. F7 ou o botão de mostrar interface para sair." : "Cinema camera on. Press F7 or the show interface button to leave.");
        }

        private static Sprite Icon()
        {
            if (_icon != null) return _icon;
            string[] rows =
            {
                "................................",
                "................................",
                "................................",
                "................................",
                "................................",
                "...........########.............",
                "..........#oooooooo#............",
                "....#######oooooooo#######......",
                "...#oooooooooooooooooooooo#.....",
                "...#oooooooo######oooooooo#.....",
                "...#ooooooo#wwwwww#ooooooo#.....",
                "...#oooooo#wwbbbbww#oooooo#.....",
                "...#oooooo#wbbbbbbw#oooooo#.....",
                "...#oooooo#wbbbbbbw#oooooo#.....",
                "...#oooooo#wwbbbbww#oooooo#.....",
                "...#ooooooo#wwwwww#ooooooo#.....",
                "...#oooooooo######oooooooo#.....",
                "...#oooooooooooooooooooooo#.....",
                "....########################....",
                "................................",
                "................................",
                "................................"
            };
            int w = 32;
            int h = rows.Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < h; y++)
            {
                string r = rows[h - 1 - y];
                for (int x = 0; x < w; x++)
                {
                    Color c = new Color(0f, 0f, 0f, 0f);
                    char ch = x < r.Length ? r[x] : '.';
                    if (ch == '#') c = new Color(0.1f, 0.1f, 0.1f, 1f);
                    else if (ch == 'o') c = new Color(0.82f, 0.82f, 0.82f, 1f);
                    else if (ch == 'w') c = new Color(0.95f, 0.95f, 0.95f, 1f);
                    else if (ch == 'b') c = new Color(0.2f, 0.45f, 0.75f, 1f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            _icon = Sprite.Create(tex, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 32f);
            return _icon;
        }
    }
}
