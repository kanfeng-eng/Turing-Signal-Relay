using System;
using System.IO;
using UnityEngine;

namespace TuringSignalRelay.Editor
{
    public static class ObserverAcceptanceChecks
    {
        [Serializable]
        private sealed class Report
        {
            public int frames = 9001, loads = 3, viewportSizes = 5;
            public long fixtureBytes;
            public double[] loadSeconds = new double[3];
            public long[] retainedBytes = new long[3];
            public long peakWorkingSetBytes;
            public bool peakWorkingSetAvailable;
            public long unityAllocatedBytes;
            public string status = "passed";
        }

        public static void Run()
        {
            ObserverInteractionChecks.Run();
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts"));
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, "observer-long-" + Guid.NewGuid() + ".json");
            var root = new GameObject("Observer long recording checks");
            bool passed = false;
            try
            {
                float[] expectedX = CreateFixture(path);
                var report = new Report { fixtureBytes = new FileInfo(path).Length };
                var controller = root.AddComponent<ObserverController>();
                controller.enabled = false;
                controller.LoadDemo();
                root.AddComponent<ObserverHud>().Initialize(controller);
                long baseline = GC.GetTotalMemory(true);
                for (int load = 0; load < report.loads; load++)
                {
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    controller.LoadReplay(path);
                    watch.Stop();
                    Require(controller.Error == "" && controller.Playback.Frames.Count == 9001, "Long recording did not load.");
                    report.loadSeconds[load] = watch.Elapsed.TotalSeconds;
                    Require(report.loadSeconds[load] < 60, "Long recording load exceeded 60 seconds.");
                    for (int jump = 0; jump < 100; jump++)
                    {
                        int tick = jump * 977 % 9001;
                        controller.Playback.Seek(tick);
                        var frame = controller.Frame;
                        Require(frame.tick == tick && frame.entities[0].x == expectedX[tick % 301] && frame.scoreHalfPoints[0] == tick / 10, "Random seek changed authoritative fixture data.");
                        if (jump % 10 == 0) ObserverInteractionChecks.Render(root);
                    }
                    controller.Playback.Seek(0);
                    controller.Playback.Speed = 1;
                    controller.Playback.Playing = true;
                    for (int tick = 1; tick <= 9000; tick++)
                    {
                        controller.Playback.Advance(.1);
                        Require(controller.Frame.tick == tick, "Sequential playback skipped a fixture tick.");
                    }
                    Require(!controller.Playback.Playing && controller.Frame.scoreHalfPoints[0] == 900, "Long recording did not stop at the correct result.");
                    controller.Playback.Seek(0);
                    controller.Playback.Speed = 4;
                    controller.Playback.Playing = true;
                    controller.Playback.Advance(225);
                    Require(controller.Frame.tick == 9000 && controller.Frame.scoreHalfPoints[0] == 900, "Speed changed the final result.");
                    ObserverInteractionChecks.Render(root);
                    report.retainedBytes[load] = GC.GetTotalMemory(true) - baseline;
                    Require(report.retainedBytes[load] < 256L * 1024 * 1024, "Long recording retained more than 256 MiB: " + report.retainedBytes[load]);
                    Debug.Log("OBSERVER_LONG_LOAD_PASSED pass=" + load + " seconds=" + report.loadSeconds[load] + " retained=" + report.retainedBytes[load]);
                }
                Require(report.retainedBytes[2] - report.retainedBytes[0] < 32L * 1024 * 1024, "Repeated loading retained prior recordings.");
                using (var process = System.Diagnostics.Process.GetCurrentProcess())
                    report.peakWorkingSetBytes = process.PeakWorkingSet64;
                report.peakWorkingSetAvailable = report.peakWorkingSetBytes > 0;
                report.unityAllocatedBytes = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                File.WriteAllText(Path.Combine(output, "observer-acceptance.json"), JsonUtility.ToJson(report, true));
                passed = true;
                Debug.Log("OBSERVER_ACCEPTANCE_CHECKS_PASSED frames=9001 loads=3");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                if (passed) File.Delete(path);
            }
        }

        private static float[] CreateFixture(string path)
        {
            var demo = new GameObject("Long fixture source");
            try
            {
                var source = demo.AddComponent<ObserverController>();
                source.enabled = false;
                source.LoadDemo();
                var expectedX = new float[301];
                for (int i = 0; i < expectedX.Length; i++) expectedX[i] = source.Playback.Frames[i].entities[0].x;
                using (var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew), new System.Text.UTF8Encoding(false)))
                {
                    writer.Write("{\"type\":\"observer-recording\",\"replayVersion\":1,\"ticksPerSecond\":10,\"frames\":[");
                    for (int tick = 0; tick <= 9000; tick++)
                    {
                        var frame = source.Playback.Frames[tick % 301];
                        frame.tick = tick;
                        frame.durationTicks = 9000;
                        frame.rulesVersion = "observer-stress-fixture";
                        frame.scoreHalfPoints = new[] { tick / 10, tick / 13 };
                        frame.weather = (tick / 1000) % 2 == 0 ? "Clear" : "Fog";
                        frame.hubProgress = tick / 9000f;
                        frame.eventFocusId = tick % 1000 == 0 ? frame.entities[0].id : -1;
                        frame.nextEventTick = Math.Min(9000, (tick / 1000 + 1) * 1000);
                        foreach (var entity in frame.entities)
                        {
                            entity.visibleToMask = (tick / 1000) % 2 == 0 ? 3 : 1 << entity.side;
                            entity.suppressed = (tick / 70) % 2 == 1;
                        }
                        foreach (var link in frame.links) link.connected = (tick / 50) % 2 == 0;
                        if (tick > 0) writer.Write(',');
                        writer.Write(JsonUtility.ToJson(frame));
                    }
                    writer.Write("]}");
                }
                return expectedX;
            }
            finally { UnityEngine.Object.DestroyImmediate(demo); }
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
