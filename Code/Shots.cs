using System;
using System.Collections;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace ChatRealms
{
    internal static class Shots
    {
        public static volatile bool Busy;
        public static volatile bool Capturing;
        public static bool Hiding;
        private const int MaxBytes = 9000000;
        private static WorldBoxConsole.Console _console;
        private static float _consoleSearch = -10f;

        private class CamState
        {
            public bool Moved;
            public bool Hide;
            public bool Restored;
            public Vector3 Pos;
            public float Size;
            public float Target;
            public float Focus;
            public Actor Followed;
            public bool ErrWas;
            public bool UiWas;
        }

        private class Job
        {
            public byte[] Raw;
            public int Width;
            public int Height;
            public int Stride;
            public byte[] Image;
            public string Error;
            public volatile bool Done;
            public volatile bool Shipped;
        }

        internal static void CloseConsole()
        {
            try
            {
                WorldBoxConsole.Console con = World.world != null ? World.world.console : null;
                if (con == null)
                {
                    if (_console == null && Time.realtimeSinceStartup - _consoleSearch > 5f)
                    {
                        _consoleSearch = Time.realtimeSinceStartup;
                        _console = UnityEngine.Object.FindObjectOfType<WorldBoxConsole.Console>(true);
                    }
                    con = _console;
                }
                if (con != null && con.isActive()) con.Hide();
            }
            catch (Exception)
            {
            }
        }

        public static void Begin(RelayRequest r, string mode, Vector3? focus, float zoom, JObject info)
        {
            Busy = true;
            Capturing = true;
            ChatRealmsMod.Instance.StartCoroutine(Run(r, mode, focus, zoom, info));
        }

        private static JObject Reply(RelayRequest r, bool ok, string code, string message, JObject data)
        {
            var o = new JObject();
            o["type"] = "result";
            o["id"] = r.Id;
            o["ok"] = ok;
            o["code"] = code;
            o["message"] = message;
            if (data != null) o["data"] = data;
            return o;
        }

        private static void Fail(RelayRequest r, string error)
        {
            ChatRealmsMod.LogWarning("screenshot: " + error);
            ChatRealmsMod.Instance.Relay.Send(Reply(r, false, "internal_error", error ?? "capture failed", null));
        }

        private static void Prepare(CamState st, string mode, Vector3? focus, float zoom)
        {
            st.ErrWas = Config.show_console_on_error;
            st.UiWas = Config.ui_main_hidden;
            Config.show_console_on_error = false;
            CloseConsole();
            st.Hide = ChatRealmsMod.Instance.Cfg.HideUiInShots;
            if (st.Hide)
            {
                Hiding = true;
                Config.ui_main_hidden = true;
            }
            try
            {
                MoveCamera cam = MoveCamera.instance;
                Camera main = cam != null ? cam.main_camera : null;
                if (main == null || !focus.HasValue) return;
                st.Pos = cam.transform.position;
                st.Size = main.orthographicSize;
                st.Target = cam._target_zoom;
                st.Focus = cam._focus_zoom;
                st.Followed = MoveCamera.getFocusUnit();
                MoveCamera.clearFocusUnitOnly();
                st.Moved = true;
                cam.transform.position = new Vector3(focus.Value.x, focus.Value.y, st.Pos.z);
                float z = Mathf.Clamp(zoom, 10f, cam.orthographic_size_max);
                if (mode == "unit") z = 10f;
                cam._target_zoom = z;
                cam._focus_zoom = z;
                main.orthographicSize = z;
                World.world.setZoomOrthographic(z);
            }
            catch (Exception e)
            {
                ChatRealmsMod.LogWarning("screenshot camera: " + e.Message);
            }
        }

        private static void Restore(CamState st)
        {
            if (st == null || st.Restored) return;
            st.Restored = true;
            Config.show_console_on_error = st.ErrWas;
            if (st.Hide)
            {
                Config.ui_main_hidden = st.UiWas;
                Hiding = false;
            }
            Capturing = false;
            if (!st.Moved) return;
            try
            {
                MoveCamera cam = MoveCamera.instance;
                Camera main = cam != null ? cam.main_camera : null;
                if (cam != null)
                {
                    cam.transform.position = st.Pos;
                    cam._target_zoom = st.Target;
                    cam._focus_zoom = st.Focus;
                }
                if (main != null) main.orthographicSize = st.Size;
                World.world.setZoomOrthographic(st.Size);
                if (st.Followed != null && st.Followed.isAlive()) MoveCamera.setFocusUnit(st.Followed);
            }
            catch (Exception)
            {
            }
        }

        private static Job Capture()
        {
            var job = new Job();
            Texture2D tex = null;
            try
            {
                int w = Screen.width;
                int h = Screen.height;
                tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                tex.Apply(false);
                byte[] raw = tex.GetRawTextureData();
                if (raw == null || w <= 0 || h <= 0 || raw.Length < w * h * 3) throw new Exception("bad capture");
                int stride = raw.Length % h == 0 && raw.Length / h >= w * 3 ? raw.Length / h : w * 3;
                job.Raw = raw;
                job.Width = w;
                job.Height = h;
                job.Stride = stride;
            }
            catch (Exception e)
            {
                job.Error = e.Message;
            }
            finally
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }
            return job;
        }

        private static void Encode(Job job)
        {
            try
            {
                int scale = Math.Max(1, Math.Min(3, 3840 / Math.Max(1, job.Width)));
                byte[] png = Png.Encode(job.Raw, job.Width, job.Height, job.Stride, scale);
                if (png.Length > MaxBytes && scale > 1) png = Png.Encode(job.Raw, job.Width, job.Height, job.Stride, 1);
                job.Image = png;
            }
            catch (Exception e)
            {
                job.Error = e.Message;
            }
            finally
            {
                job.Done = true;
            }
        }

        private static byte[] Jpeg(Job job)
        {
            Texture2D tex = null;
            try
            {
                tex = new Texture2D(job.Width, job.Height, TextureFormat.RGB24, false);
                tex.LoadRawTextureData(job.Raw);
                tex.Apply(false);
                byte[] best = null;
                for (int q = 95; q >= 65; q -= 10)
                {
                    best = ImageConversion.EncodeToJPG(tex, q);
                    if (best.Length <= MaxBytes) break;
                }
                return best;
            }
            catch (Exception e)
            {
                job.Error = e.Message;
                return null;
            }
            finally
            {
                if (tex != null) UnityEngine.Object.Destroy(tex);
            }
        }

        private static void Ship(Job job, RelayRequest r, string mode, string mime, byte[] img, JObject info)
        {
            RelayClient relay = ChatRealmsMod.Instance.Relay;
            string id = r.Id;
            JObject d = info ?? new JObject();
            d["mode"] = mode;
            d["mime"] = mime;
            var t = new Thread(() =>
            {
                try
                {
                    d["image"] = Convert.ToBase64String(img);
                    var o = new JObject();
                    o["type"] = "result";
                    o["id"] = id;
                    o["ok"] = true;
                    o["code"] = "ok";
                    o["message"] = "screenshot";
                    o["data"] = d;
                    relay.SendRaw(o.ToString(Formatting.None));
                }
                catch (Exception)
                {
                }
                finally
                {
                    job.Shipped = true;
                }
            });
            t.IsBackground = true;
            t.Start();
        }

        private static IEnumerator Run(RelayRequest r, string mode, Vector3? focus, float zoom, JObject info)
        {
            var st = new CamState();
            bool answered = false;
            try
            {
                Prepare(st, mode, focus, zoom);
                yield return new WaitForSecondsRealtime(st.Moved ? 1.1f : (st.Hide ? 0.4f : 0.05f));
                CloseConsole();
                yield return new WaitForEndOfFrame();
                CloseConsole();
                Job job = Capture();
                Restore(st);
                if (job.Raw == null)
                {
                    Fail(r, job.Error);
                    answered = true;
                    yield break;
                }
                var t = new Thread(() => Encode(job));
                t.IsBackground = true;
                t.Start();
                while (!job.Done) yield return null;
                byte[] img = job.Image;
                string mime = "image/png";
                if (img == null || img.Length > MaxBytes)
                {
                    byte[] jpg = Jpeg(job);
                    if (jpg != null)
                    {
                        img = jpg;
                        mime = "image/jpeg";
                    }
                }
                job.Raw = null;
                if (img == null || img.Length > MaxBytes)
                {
                    Fail(r, job.Error ?? "image too large");
                    answered = true;
                    yield break;
                }
                Ship(job, r, mode, mime, img, info);
                answered = true;
                while (!job.Shipped) yield return null;
            }
            finally
            {
                Restore(st);
                if (!answered) Fail(r, "capture failed");
                Capturing = false;
                Busy = false;
            }
        }
    }
}
