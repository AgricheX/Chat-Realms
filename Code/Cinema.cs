using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChatRealms
{
    internal static class Cinema
    {
        public static bool Active;

        private class Cand
        {
            public Vector3 Pos;
            public float Score;
            public float Zoom;
            public long Follow;
        }

        private class Cluster
        {
            public float Sum;
            public int Count;
            public Vector3 Pos;
            public float MinX = float.MaxValue;
            public float MaxX = float.MinValue;
            public float MinY = float.MaxValue;
            public float MaxY = float.MinValue;
        }

        private class Rep
        {
            public Vector3 Pos;
            public float Score;
            public float Zoom;
            public float Expire;
            public long Follow;
        }

        private static readonly Dictionary<string, float> Scores = new Dictionary<string, float>
        {
            { "unit_create", 3.5f }, { "unit_enlist", 3f }, { "unit_family", 4f }, { "unit_clan", 4f }, { "unit_found", 6f },
            { "unit_duel", 7f }, { "unit_marry", 5f }, { "unit_love", 4f }, { "unit_warcry", 5f }, { "unit_combat", 5f },
            { "unit_decree", 6f }, { "unit_meta", 4.5f }, { "unit_equip", 3f }, { "unit_equip_troops", 4.5f }, { "unit_clan_manage", 3f },
            { "unit_renegade", 4f }, { "unit_say", 2f }, { "unit_info", 0.5f }
        };

        private static readonly List<Rep> _reps = new List<Rep>();
        private static bool _focus;
        private static Vector3 _target;
        private static long _follow;
        private static Vector3 _waypoint;
        private static Vector3 _vel;
        private static float _zoomGoal = 55f;
        private static float _curScore;
        private static float _scan;
        private static float _pause;
        private static float _linger;
        private static float _roamTime;
        private static float _focusTime;
        private static float _lastHot;
        private static bool _uiWas;
        private static bool _errWas;
        private static bool _hasWaypoint;

        public static void Toggle()
        {
            if (Active) Stop();
            else Start();
        }

        private static void Start()
        {
            if (!GameOps.WorldReady) return;
            Active = true;
            _uiWas = Config.ui_main_hidden;
            _errWas = Config.show_console_on_error;
            Config.ui_main_hidden = true;
            Config.show_console_on_error = false;
            _focus = false;
            _hasWaypoint = false;
            _vel = Vector3.zero;
            _roamTime = 10f;
            _scan = 0f;
            _pause = 0f;
            Shots.CloseConsole();
        }

        private static void Stop()
        {
            Active = false;
            Config.ui_main_hidden = _uiWas;
            Config.show_console_on_error = _errWas;
        }

        public static void Report(Vector3 pos, float score, float zoom = 32f, long follow = 0L)
        {
            if (!Active) return;
            _reps.Add(new Rep { Pos = pos, Score = score, Zoom = zoom, Expire = Time.realtimeSinceStartup + 45f, Follow = follow });
            if (_reps.Count > 40) _reps.RemoveAt(0);
        }

        public static void ReportUnit(Actor u, string action)
        {
            if (!Active || u == null || !u.isAlive()) return;
            float s;
            if (!Scores.TryGetValue(action, out s)) s = 2.5f;
            if (u.isKing()) s += 1.5f;
            else if (u.isCityLeader()) s += 0.8f;
            Report(u.cur_transform_position, s, 26f, u.getID());
        }

        public static void ReportCity(City c, float score)
        {
            if (!Active || c == null) return;
            WorldTile t = c.getTile();
            if (t == null) return;
            Report(new Vector3(t.x, t.y, 0f), score, 50f);
        }

        public static void ReportKingdom(Kingdom k, float score)
        {
            if (!Active || k == null || k.capital == null) return;
            ReportCity(k.capital, score);
        }

        private static List<Cand> Build()
        {
            var list = new List<Cand>();
            var grid = new Dictionary<long, Cluster>();
            foreach (Actor a in World.world.units)
            {
                if (a == null || !a.isAlive() || !a.isFighting()) continue;
                bool civ = a.kingdom != null && a.kingdom.isCiv();
                float w = civ ? 1f : 0.3f;
                if (civ && World.world.wars.hasWars(a.kingdom)) w += 1.5f;
                if (a.isKing()) w += 2f;
                if (a.data.favorite) w += 2f;
                w += Mathf.Min(a.data.level, 30) / 10f;
                Vector3 p = a.cur_transform_position;
                long key = (long)Mathf.FloorToInt(p.x / 14f) * 100000L + Mathf.FloorToInt(p.y / 14f);
                Cluster c;
                if (!grid.TryGetValue(key, out c))
                {
                    c = new Cluster();
                    grid[key] = c;
                }
                c.Sum += w;
                c.Count++;
                c.Pos += p;
                c.MinX = Mathf.Min(c.MinX, p.x);
                c.MaxX = Mathf.Max(c.MaxX, p.x);
                c.MinY = Mathf.Min(c.MinY, p.y);
                c.MaxY = Mathf.Max(c.MaxY, p.y);
            }
            foreach (Cluster c in grid.Values)
            {
                if (c.Count < 2) continue;
                float spread = Mathf.Max(c.MaxX - c.MinX, c.MaxY - c.MinY);
                float score = c.Sum * (c.Count == 2 ? 1.6f : 1f);
                list.Add(new Cand { Pos = c.Pos / c.Count, Score = Mathf.Min(score, 30f), Zoom = Mathf.Clamp(spread * 0.9f + 20f, 20f, 60f) });
            }
            float now = Time.realtimeSinceStartup;
            _reps.RemoveAll(r => r.Expire < now);
            foreach (Rep r in _reps)
            {
                if (r.Follow != 0L)
                {
                    Actor fa = World.world.units.get(r.Follow);
                    if (fa != null && fa.isAlive()) r.Pos = fa.cur_transform_position;
                    else r.Follow = 0L;
                }
                float fade = Mathf.Clamp01((r.Expire - now) / 45f);
                list.Add(new Cand { Pos = r.Pos, Score = r.Score * (0.5f + 0.5f * fade), Zoom = r.Zoom, Follow = r.Follow });
            }
            return list;
        }

        private static Vector3 RandomWaypoint(Vector3 from)
        {
            float w = Mathf.Max(60f, MapBox.width);
            float h = Mathf.Max(60f, MapBox.height);
            Vector3 best = from;
            float bestScore = -1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 c;
                float roll = UnityEngine.Random.value;
                var kings = GameOps.CivKingdoms();
                if (roll < 0.55f && kings.Count > 0)
                {
                    Kingdom k = kings[UnityEngine.Random.Range(0, kings.Count)];
                    if (k.cities.Count == 0) continue;
                    WorldTile t = k.cities[UnityEngine.Random.Range(0, k.cities.Count)].getTile();
                    if (t == null) continue;
                    c = new Vector3(t.x, t.y, 0f);
                }
                else
                {
                    c = new Vector3(UnityEngine.Random.Range(w * 0.08f, w * 0.92f), UnityEngine.Random.Range(h * 0.08f, h * 0.92f), 0f);
                }
                float d = Vector3.Distance(c, from);
                float score = Mathf.Min(d, 220f) + UnityEngine.Random.value * 30f;
                if (d < 50f) score *= 0.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = c;
                }
            }
            return best;
        }

        private static void EnterFocus(Cand c)
        {
            _focus = true;
            _target = c.Pos;
            _follow = c.Follow;
            _zoomGoal = c.Zoom;
            _curScore = c.Score;
            _focusTime = 0f;
            _lastHot = Time.realtimeSinceStartup;
        }

        private static void EnterRoam(Vector3 from)
        {
            _focus = false;
            _follow = 0L;
            _roamTime = 0f;
            _waypoint = RandomWaypoint(from);
            _hasWaypoint = true;
            _linger = 0f;
            _zoomGoal = UnityEngine.Random.Range(48f, 70f);
        }

        private static void Scan(Vector3 camPos)
        {
            List<Cand> list = Build();
            Cand best = null;
            foreach (Cand c in list)
            {
                if (best == null || c.Score > best.Score) best = c;
            }
            float now = Time.realtimeSinceStartup;
            if (!_focus)
            {
                if (best != null && best.Score >= 3f && _roamTime > 6f)
                {
                    float chance = Mathf.Clamp01((best.Score - 2f) / 11f) * 0.7f + 0.1f;
                    if (UnityEngine.Random.value < chance) EnterFocus(best);
                }
                return;
            }
            Cand near = null;
            foreach (Cand c in list)
            {
                if (Vector3.Distance(c.Pos, _target) < 42f && (near == null || c.Score > near.Score)) near = c;
            }
            if (near != null && near.Score >= 2f)
            {
                _target = near.Pos;
                _follow = near.Follow;
                _curScore = near.Score;
                _zoomGoal = near.Zoom;
                _lastHot = now;
            }
            if (best != null && _focusTime > 8f && best.Score > _curScore * 1.8f && Vector3.Distance(best.Pos, _target) > 42f) EnterFocus(best);
            if (now - _lastHot > 7f || _focusTime > 70f) EnterRoam(camPos);
        }

        public static void Tick(float dt)
        {
            if (!Active) return;
            if (!GameOps.WorldReady)
            {
                Stop();
                return;
            }
            if (!Config.ui_main_hidden)
            {
                Stop();
                return;
            }
            Shots.CloseConsole();
            Config.show_console_on_error = false;
            if (Shots.Capturing) return;
            MoveCamera cam = MoveCamera.instance;
            Camera main = cam != null ? cam.main_camera : null;
            if (main == null) return;

            if (Input.GetAxis("Mouse ScrollWheel") != 0f || Input.GetMouseButton(1) || Input.GetMouseButton(2)) _pause = 6f;
            if (_pause > 0f)
            {
                _pause -= dt;
                _vel = Vector3.zero;
                return;
            }

            Vector3 pos = cam.transform.position;
            if (!_hasWaypoint && !_focus) EnterRoam(pos);
            if (_focus && _follow != 0L)
            {
                Actor fu = World.world.units.get(_follow);
                if (fu != null && fu.isAlive()) _target = fu.cur_transform_position;
                else _follow = 0L;
            }
            if (_focus) _focusTime += dt;
            else _roamTime += dt;
            _scan += dt;
            if (_scan >= (_focus ? 0.8f : 1.5f))
            {
                _scan = 0f;
                try
                {
                    Scan(pos);
                }
                catch (Exception e)
                {
                    ChatRealmsMod.LogWarning("cinema: " + e.Message);
                }
            }

            float t = Time.realtimeSinceStartup;
            Vector3 flat = new Vector3(pos.x, pos.y, 0f);
            Vector3 desired;
            if (_focus)
            {
                Vector3 to = _target - flat;
                float dist = to.magnitude;
                float cruise = Mathf.Clamp(dist * 1.6f, 0f, 55f);
                desired = dist > 0.01f ? to / dist * cruise : Vector3.zero;
                desired += new Vector3(Mathf.Sin(t * 0.5f), Mathf.Cos(t * 0.41f), 0f) * 1.2f;
            }
            else
            {
                Vector3 to = _waypoint - flat;
                float dist = to.magnitude;
                if (dist < 5f)
                {
                    if (_linger <= 0f) _linger = UnityEngine.Random.Range(2.5f, 4.5f);
                    _linger -= dt;
                    if (_linger <= 0f) EnterRoam(flat);
                    desired = new Vector3(Mathf.Sin(t * 0.6f), Mathf.Cos(t * 0.5f), 0f) * 1.5f;
                }
                else
                {
                    float cruise = Mathf.Min(9f, 1.5f + dist * 0.25f);
                    desired = to / dist * cruise;
                }
            }
            float accel = 1f - Mathf.Exp(-dt * (_focus ? 2.4f : 1.1f));
            _vel = Vector3.Lerp(_vel, desired, accel);
            Vector3 next = flat + _vel * dt;
            next.x = Mathf.Clamp(next.x, 0f, MapBox.width);
            next.y = Mathf.Clamp(next.y, 0f, MapBox.height);
            cam.transform.position = new Vector3(next.x, next.y, pos.z);

            float breath = 1f + 0.05f * Mathf.Sin(t * 0.45f);
            float zGoal = Mathf.Clamp(_zoomGoal * breath, 10f, cam.orthographic_size_max);
            float z = Mathf.Lerp(main.orthographicSize, zGoal, 1f - Mathf.Exp(-dt * 1.2f));
            cam._target_zoom = z;
            cam._focus_zoom = z;
            main.orthographicSize = z;
            World.world.setZoomOrthographic(z);
        }
    }
}
