using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace TuringSignalRelay.Editor
{
    public static class ObserverChecks
    {
        public static void Run()
        {
            var asset = Resources.Load<TextAsset>("ObserverDemo");
            string json;
            using (var input = new MemoryStream(asset.bytes))
            using (var zip = new GZipStream(input, CompressionMode.Decompress))
            using (var reader = new StreamReader(zip))
                json = reader.ReadToEnd();
            var player = new ObserverPlayback();
            player.Load(json);
            if (player.Frames.Count != 301)
                throw new Exception("Fixture frame count.");
            player.Playing = true;
            player.Advance(.3);
            if (player.Current.tick != 3)
                throw new Exception("Playback timing.");
            player.Playing = false;
            player.Advance(100);
            if (player.Current.tick != 3)
                throw new Exception("Pause advanced.");
            player.Step();
            if (player.Current.tick != 4)
                throw new Exception("Single-step.");
            player.Seek(200);
            var expected = player.Current.scoreHalfPoints[0];
            player.Seek(0);
            player.Playing = true;
            player.Speed = 4;
            player.Advance(5);
            if (player.Current.tick != 200 || player.Current.scoreHalfPoints[0] != expected)
                throw new Exception("Seek/speed mismatch.");
            var live = new LiveFrameBuffer();
            live.Accept(player.Frames[100]);
            if (live.Accept(player.Frames[90]) || live.Accept(player.Frames[100]) || !live.Accept(player.Frames[101]))
                throw new Exception("Live rollback.");
            var response = new ObserverSnapshotResponse { protocolVersion = 1, tick = 102, status = "running", snapshot = player.Frames[102] };
            if (!live.AcceptJson(JsonUtility.ToJson(response)) || live.Status != "running")
                throw new Exception("Snapshot envelope failed.");
            var frame = player.Frames[0];
            foreach (var entity in frame.entities)
                if (frame.Visible(entity, 1 - entity.side))
                    throw new Exception("Hidden unit leaked into other side.");
            bool rejected = false;
            try
            {
                player.Load("{\"type\":\"observer-recording\",\"replayVersion\":99}");
            }
            catch (InvalidDataException) { rejected = true; }
            if (!rejected || player.Current.tick != 200)
                throw new Exception("Failed load mutated playback.");
            var badFrame = JsonUtility.FromJson<ObserverFrame>(JsonUtility.ToJson(player.Frames[0]));
            badFrame.protocolVersion = 99;
            rejected = false;
            try
            {
                live.Accept(badFrame);
            }
            catch (ArgumentException) { rejected = true; }
            if (!rejected || live.Current.tick != 102)
                throw new Exception("Invalid live data changed buffer.");
            string replayPath = Path.Combine(Path.GetTempPath(), "observer-full-" + Guid.NewGuid() + ".jsonl");
            try
            {
                var config = new MatchConfig();
                var engine = new MatchEngine(config);
                using (var writer = new BootstrapReplayWriter(replayPath, config))
                {
                    writer.Write(engine.GetSnapshot());
                    while (!engine.Clock.Completed)
                        engine.Step(writer.Write);
                }
                player.LoadBootstrap(replayPath);
                player.Seek(9000);
                if (player.Frames.Count != 9001 || player.Current.tick != 9000 || player.Current.scoreHalfPoints[0] != 0)
                    throw new Exception("Full-duration replay failed.");
            }
            finally { File.Delete(replayPath); }
            var uiRoot = new GameObject("Observer UI checks");
            try
            {
                var controller = uiRoot.AddComponent<ObserverController>();
                controller.enabled = false;
                controller.LoadDemo();
                uiRoot.AddComponent<ObserverHud>().Initialize(controller);
                var buttons = uiRoot.GetComponentsInChildren<UnityEngine.UI.Button>(true);
                if (buttons.Length != 14)
                    throw new Exception("Observer controls missing.");
                Array.Find(buttons, button => button.name == "Play").onClick.Invoke();
                controller.Playback.Advance(.2);
                Array.Find(buttons, button => button.name == "Step +1").onClick.Invoke();
                if (controller.Playback.Index != 3 || controller.Playback.Playing)
                    throw new Exception("UI play/step binding failed.");
                Array.Find(buttons, button => button.name == "Red view").onClick.Invoke();
                if (controller.Side != 0)
                    throw new Exception("UI side binding failed.");
                if (uiRoot.GetComponentsInChildren<UnityEngine.UI.InputField>(true).Length != 3)
                    throw new Exception("Input fields missing.");
                var slider = uiRoot.GetComponentInChildren<UnityEngine.UI.Slider>();
                slider.maxValue = 300;
                slider.value = 100;
                if (controller.Playback.Index != 100)
                    throw new Exception("Timeline binding failed.");
                Canvas.ForceUpdateCanvases();
                foreach (var map in uiRoot.GetComponentsInChildren<ObserverMap>())
                    map.SendMessage("LateUpdate");
                var terrainViews = uiRoot.GetComponentsInChildren<UnityEngine.UI.RawImage>();
                if (terrainViews.Length != 2 || terrainViews[0].texture == null || terrainViews[1].texture == null)
                    throw new Exception("Native map rendering was not initialized.");
                Debug.Log("OBSERVER_UI_CHECKS_PASSED buttons=" + buttons.Length);
            }
            finally { UnityEngine.Object.DestroyImmediate(uiRoot); }
            int count = 0;
            foreach (string name in new[] { "police", "crime", "student", "straight", "book", "wall1" })
            {
                var texture = Resources.Load<Texture2D>("LegacySprites/" + name);
                if (texture == null)
                    continue;
                if (Resources.Load<Sprite>("LegacySprites/" + name) == null || Resources.Load<Sprite>("LegacySprites/" + name).rect.width != 256 || texture.width != 256 || texture.height != 256)
                    throw new Exception("Missing sprite " + name);
                count++;
            }
            Debug.Log("OBSERVER_CHECKS_PASSED frames=" + player.Frames.Count + " sprites=" + count);
        }
    }
}
