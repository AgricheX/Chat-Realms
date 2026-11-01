using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ChatRealms
{
    internal static class ModUi
    {
        private static GameObject _root;
        private static Canvas _canvas;
        private static Font _font;
        private static Sprite _panel;
        private static Sprite _inset;
        private static readonly Dictionary<string, Sprite> _icons = new Dictionary<string, Sprite>();

        private static readonly Color Ink = new Color(0.93f, 0.92f, 0.86f, 1f);
        private static readonly Color Dim = new Color(0.66f, 0.67f, 0.62f, 1f);
        private static readonly Color Accent = new Color(0.96f, 0.6f, 0.14f, 1f);
        private static readonly Color Good = new Color(0.42f, 0.78f, 0.34f, 1f);
        private static readonly Color Bad = new Color(0.86f, 0.3f, 0.26f, 1f);

        private static readonly Dictionary<string, string> IconOf = new Dictionary<string, string>
        {
            { "disaster_tornado", "iconTornado" }, { "disaster_heatwave", "iconSunblessed" }, { "disaster_small_meteorite", "iconMeteorite" },
            { "disaster_small_earthquake", "iconEarthquake" }, { "disaster_hellspawn", "iconFireSkull" }, { "disaster_ice_ones_awoken", "iconIceberg" },
            { "disaster_sudden_snowman", "iconSnowMan" }, { "disaster_garden_surprise", "icon_grow_plants_fast" }, { "disaster_dragon_from_farlands", "iconDragon" },
            { "disaster_ash_bandits", "iconAshFever" }, { "disaster_alien_invasion", "iconAlien" }, { "disaster_biomass", "iconBiomass" },
            { "disaster_tumor", "iconTumor" }, { "disaster_wild_mage", "iconMageSlayer" }, { "disaster_underground_necromancer", "iconNecromancer" },
            { "disaster_mad_thoughts", "iconMadness" }, { "disaster_greg_abominations", "iconGreg" },
            { "age_hope", "iconAgeHope" }, { "age_sun", "iconAgeSun" }, { "age_moon", "iconAgeMoon" }, { "age_wonders", "iconAgeWonders" },
            { "age_dark", "iconAgeDark" }, { "age_tears", "iconAgeTears" }, { "age_chaos", "iconAgeChaos" }, { "age_ice", "iconAgeIce" },
            { "age_ash", "iconAgeAsh" }, { "age_despair", "iconAgeDespair" },
            { "k_bless", "iconBlessing" }, { "k_inspire", "iconInspiration" }, { "k_cure", "iconHealingAura" }, { "k_shield", "iconShield" },
            { "k_friendship", "iconFriendship" }, { "k_rain", "iconRain" }, { "k_grow", "icon_grow_plants_fast" }, { "k_party", "iconFireworks" },
            { "k_curse", "iconCursed" }, { "k_plague", "iconPlague" }, { "k_madness", "iconMadness" }, { "k_fire", "iconFire" },
            { "k_discord", "iconRage" }, { "k_spite", "iconAngry" }, { "k_bomb", "iconBomb" }, { "k_acid", "iconAcid" },
            { "k_gold", "iconMoney" }, { "k_wheat", "iconStarving" }, { "k_reinforce", "iconArmy" }, { "k_war", "iconWar" }, { "k_peace", "iconPeaceful" }
        };

        private class Row
        {
            public Image Bar;
            public Text Votes;
            public RectTransform BarRt;
        }

        private static RectTransform _pollRt;
        private static Text _pollTitle;
        private static Text _pollTimer;
        private static Image _pollTime;
        private static Image _pollIcon;
        private static readonly List<Row> _rows = new List<Row>();
        private static string _pollKey = "";
        private static float _rowW;

        private static RectTransform _statusRt;
        private static Text _statusText;
        private static float _statusTick;

        private class ToastItem
        {
            public string Text;
            public string Icon;
            public float Life;
        }

        private static readonly Queue<ToastItem> _toasts = new Queue<ToastItem>();
        private static RectTransform _toastRt;
        private static CanvasGroup _toastGroup;
        private static Text _toastText;
        private static Image _toastIcon;
        private static float _toastLeft;
        private static float _toastLife;
        private static bool _failed;

        public static void Toast(string text, string icon = null, float life = 4.5f)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_failed)
            {
                try { WorldTip.showNow(text, false, "top", 5f); } catch (Exception) { }
                return;
            }
            if (_toasts.Count > 6) _toasts.Dequeue();
            _toasts.Enqueue(new ToastItem { Text = text, Icon = icon, Life = life });
        }

        private static bool Ensure()
        {
            if (_failed) return false;
            if (_root != null) return true;
            try
            {
                _root = new GameObject("ChatRealmsUI");
                UnityEngine.Object.DontDestroyOnLoad(_root);
                _canvas = _root.AddComponent<Canvas>();
                _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                _canvas.sortingOrder = 400;
                _root.AddComponent<GraphicRaycaster>();
                var scaler = _root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
                _font = FindFont();
                _panel = MakeSprite(new Color(0.2f, 0.21f, 0.19f, 0.96f), new Color(0.04f, 0.04f, 0.04f, 1f), new Color(0.36f, 0.37f, 0.33f, 1f), new Color(0.12f, 0.12f, 0.11f, 1f));
                _inset = MakeSprite(new Color(0.12f, 0.13f, 0.12f, 0.95f), new Color(0.05f, 0.05f, 0.05f, 1f), new Color(0.17f, 0.18f, 0.16f, 1f), new Color(0.2f, 0.21f, 0.19f, 1f));
                BuildPoll();
                BuildToast();
                BuildStatus();
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                ChatRealmsMod.LogWarning("ui: " + e.Message);
                if (_root != null) UnityEngine.Object.Destroy(_root);
                _root = null;
                return false;
            }
        }

        private static Font FindFont()
        {
            try
            {
                Text[] all = Resources.FindObjectsOfTypeAll<Text>();
                foreach (Text t in all)
                {
                    if (t != null && t.font != null && t.font.name.ToLowerInvariant().Contains("droid")) return t.font;
                }
                foreach (Text t in all)
                {
                    if (t != null && t.font != null) return t.font;
                }
            }
            catch (Exception)
            {
            }
            return Font.CreateDynamicFontFromOSFont("Arial", 16);
        }

        private static Sprite MakeSprite(Color fill, Color edge, Color light, Color shade)
        {
            const int n = 16;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    int dx = Math.Min(x, n - 1 - x);
                    int dy = Math.Min(y, n - 1 - y);
                    int d = Math.Min(dx, dy);
                    Color c = fill;
                    if (d == 0) c = edge;
                    else if (d == 1) c = (y >= n / 2 ? light : shade);
                    bool corner = dx == 0 && dy == 0;
                    if (corner) c = new Color(0f, 0f, 0f, 0f);
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 4, 4, 4));
        }

        private static Sprite Icon(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            Sprite s;
            if (_icons.TryGetValue(name, out s)) return s;
            s = null;
            string[] paths = { "ui/Icons/" + name, "ui/icons/" + name, "ui/Icons/actor_traits/" + name, "ui/Icons/ages/" + name, "ui/Icons/worldrules/" + name, "ui/Icons/items/" + name, "ui/Icons/skills/" + name, name };
            foreach (string path in paths)
            {
                try { s = SpriteTextureLoader.getSprite(path); } catch (Exception) { s = null; }
                if (s != null) break;
            }
            _icons[name] = s;
            return s;
        }

        private static RectTransform Rt(GameObject go, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static Image Box(Transform parent, Sprite sprite, Color color, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            Rt(go, parent, anchor, pivot, pos, size);
            var img = go.AddComponent<Image>();
            img.sprite = sprite;
            img.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        private static Text Label(Transform parent, string text, int size, Color color, TextAnchor align, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 box)
        {
            var go = new GameObject("t", typeof(RectTransform));
            Rt(go, parent, anchor, pivot, pos, box);
            var t = go.AddComponent<Text>();
            t.font = _font;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.text = text;
            var sh = go.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.8f);
            sh.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        private static readonly Vector2 TL = new Vector2(0f, 1f);
        private static readonly Vector2 TC = new Vector2(0.5f, 1f);
        private static readonly Vector2 BL = new Vector2(0f, 0f);
        private static readonly Vector2 BR = new Vector2(1f, 0f);

        private static void BuildPoll()
        {
            var img = Box(_root.transform, _panel, Color.white, TL, TL, new Vector2(24f, -110f), new Vector2(440f, 200f), "poll");
            _pollRt = img.rectTransform;
            _pollIcon = Box(_pollRt, null, Color.white, TL, TL, new Vector2(14f, -10f), new Vector2(30f, 30f), "icon");
            _pollTitle = Label(_pollRt, "", 26, Ink, TextAnchor.MiddleLeft, TL, TL, new Vector2(52f, -10f), new Vector2(280f, 30f));
            _pollTimer = Label(_pollRt, "", 26, Accent, TextAnchor.MiddleRight, TL, TL, new Vector2(330f, -10f), new Vector2(96f, 30f));
            _pollTime = Box(_pollRt, _inset, Accent, TL, TL, new Vector2(14f, -46f), new Vector2(412f, 8f), "time");
            _pollRt.gameObject.SetActive(false);
        }

        private static void RebuildRows(PollManager p)
        {
            foreach (var r in _rows)
            {
                if (r.Bar != null) UnityEngine.Object.Destroy(r.Bar.transform.parent.gameObject);
            }
            _rows.Clear();
            _rowW = 412f;
            float y = -62f;
            for (int i = 0; i < p.Options.Count; i++)
            {
                EventDef def = p.Options[i];
                var back = Box(_pollRt, _inset, Color.white, TL, TL, new Vector2(14f, y), new Vector2(_rowW, 36f), "row");
                var fill = Box(back.transform, _panel, new Color(0.96f, 0.6f, 0.14f, 0.55f), TL, TL, new Vector2(2f, -2f), new Vector2(0f, 32f), "fill");
                Label(back.transform, (i + 1).ToString(), 24, Accent, TextAnchor.MiddleCenter, TL, TL, new Vector2(6f, -3f), new Vector2(28f, 30f));
                string iconName;
                if (IconOf.TryGetValue(def.Id, out iconName))
                {
                    Sprite s = Icon(iconName);
                    if (s != null)
                    {
                        var ic = Box(back.transform, null, Color.white, TL, TL, new Vector2(38f, -4f), new Vector2(28f, 28f), "ic");
                        ic.sprite = s;
                        ic.type = Image.Type.Simple;
                        ic.preserveAspect = true;
                    }
                }
                Label(back.transform, def.Name(ChatRealmsMod.Instance.Cfg.Portuguese), 22, Ink, TextAnchor.MiddleLeft, TL, TL, new Vector2(74f, -3f), new Vector2(270f, 30f));
                var votes = Label(back.transform, "0", 24, Ink, TextAnchor.MiddleRight, TL, TL, new Vector2(340f, -3f), new Vector2(64f, 30f));
                _rows.Add(new Row { Bar = fill, BarRt = fill.rectTransform, Votes = votes });
                y -= 42f;
            }
            _pollRt.sizeDelta = new Vector2(440f, -y + 14f);
        }

        private static void BuildToast()
        {
            var img = Box(_root.transform, _panel, Color.white, TC, TC, new Vector2(0f, -40f), new Vector2(520f, 54f), "toast");
            _toastRt = img.rectTransform;
            _toastGroup = img.gameObject.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;
            _toastGroup.interactable = false;
            _toastIcon = Box(_toastRt, null, Color.white, TL, TL, new Vector2(12f, -11f), new Vector2(32f, 32f), "icon");
            _toastText = Label(_toastRt, "", 26, Ink, TextAnchor.MiddleLeft, TL, TL, new Vector2(54f, -12f), new Vector2(800f, 30f));
            _toastRt.gameObject.SetActive(false);
        }

        private static void BuildStatus()
        {
            var img = Box(_root.transform, _panel, Color.white, BL, BL, new Vector2(24f, 24f), new Vector2(460f, 92f), "status");
            _statusRt = img.rectTransform;
            _statusText = Label(_statusRt, "", 22, Ink, TextAnchor.UpperLeft, TL, TL, new Vector2(14f, -10f), new Vector2(436f, 76f));
            _statusRt.gameObject.SetActive(false);
        }

        private static string Pick(string a, string b)
        {
            return ChatRealmsMod.Instance.Cfg.Portuguese ? a : b;
        }

        private static Color StateColor(string status)
        {
            if (status.StartsWith("paired") || status == "connected" || status == "running" || status.StartsWith("joined")) return Good;
            if (status.StartsWith("error") || status.StartsWith("missing")) return Bad;
            return Accent;
        }

        private static string Hex(Color c)
        {
            return ColorUtility.ToHtmlStringRGB(c);
        }

        public static void Tick(Settings cfg, PollManager polls, TwitchChat twitch, RelayClient relay, bool showStatus, float dt)
        {
            if (!Ensure()) return;
            _canvas.enabled = !Shots.Hiding;
            if (Shots.Hiding) return;

            bool voting = cfg.ShowOverlay && polls.Voting;
            if (_pollRt.gameObject.activeSelf != voting) _pollRt.gameObject.SetActive(voting);
            if (voting)
            {
                string key = polls.Options.Count + ":" + (polls.Options.Count > 0 ? polls.Options[0].Id : "") + ":" + (polls.Options.Count > 0 ? polls.Options[polls.Options.Count - 1].Id : "") + cfg.Portuguese;
                if (key != _pollKey)
                {
                    _pollKey = key;
                    RebuildRows(polls);
                }
                string head = polls.RoleplayPoll && polls.Target != null ? polls.Target.Label : Pick("Votação do chat", "Chat vote");
                _pollTitle.text = head;
                _pollTimer.text = Mathf.CeilToInt(polls.TimeLeft) + "s";
                var tr = _pollTime.rectTransform;
                float frac = Mathf.Clamp01(polls.TimeLeft / Mathf.Max(1f, cfg.PollDuration));
                tr.sizeDelta = new Vector2(412f * frac, 8f);
                _pollIcon.sprite = Icon("iconWar");
                _pollIcon.enabled = _pollIcon.sprite != null;
                int total = 0;
                int best = 0;
                for (int i = 0; i < polls.Counts.Length; i++)
                {
                    total += polls.Counts[i];
                    if (polls.Counts[i] > polls.Counts[best]) best = i;
                }
                for (int i = 0; i < _rows.Count && i < polls.Counts.Length; i++)
                {
                    float f = total > 0 ? (float)polls.Counts[i] / total : 0f;
                    _rows[i].BarRt.sizeDelta = new Vector2((_rowW - 4f) * f, 32f);
                    _rows[i].Bar.color = total > 0 && i == best ? new Color(0.96f, 0.6f, 0.14f, 0.75f) : new Color(0.55f, 0.57f, 0.5f, 0.55f);
                    _rows[i].Votes.text = polls.Counts[i].ToString();
                }
            }
            else
            {
                _pollKey = "";
            }

            TickToast(dt);

            if (_statusRt.gameObject.activeSelf != showStatus) _statusRt.gameObject.SetActive(showStatus);
            if (showStatus)
            {
                _statusTick -= dt;
                if (_statusTick <= 0f)
                {
                    _statusTick = 0.4f;
                    string t = cfg.TwitchEnabled ? twitch.Status : "off";
                    string d = relay.Status;
                    _statusText.supportRichText = true;
                    _statusText.text = "<color=#" + Hex(Accent) + ">Twitch</color>  <color=#" + Hex(cfg.TwitchEnabled ? StateColor(t) : Dim) + ">" + t + "</color>  #" + twitch.Channel +
                                       "\n<color=#" + Hex(Accent) + ">Discord</color>  <color=#" + Hex(StateColor(d)) + ">" + d + "</color>";
                }
            }
        }

        private static void TickToast(float dt)
        {
            if (_toastLeft <= 0f)
            {
                if (_toasts.Count == 0)
                {
                    if (_toastRt.gameObject.activeSelf) _toastRt.gameObject.SetActive(false);
                    return;
                }
                ToastItem it = _toasts.Dequeue();
                _toastText.text = it.Text;
                Sprite s = Icon(it.Icon ?? "iconCrown");
                _toastIcon.sprite = s;
                _toastIcon.enabled = s != null;
                _toastIcon.preserveAspect = true;
                float w = Mathf.Clamp(_toastText.preferredWidth + 84f, 260f, 1100f);
                _toastRt.sizeDelta = new Vector2(w, 54f);
                _toastRt.gameObject.SetActive(true);
                _toastLeft = it.Life;
                _toastLife = it.Life;
            }
            _toastLeft -= dt;
            float t = _toastLife - _toastLeft;
            float a = Mathf.Clamp01(Mathf.Min(t / 0.25f, _toastLeft / 0.4f));
            _toastGroup.alpha = a;
            _toastRt.anchoredPosition = new Vector2(0f, -40f - (1f - Mathf.Clamp01(t / 0.25f)) * 24f);
        }
    }
}
