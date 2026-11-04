using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace TuringSignalRelay.Editor
{
    // Drives the production polling iterator while the Editor pumps real HTTP operations.
    public static class ObserverNetworkChecks
    {
        public static async void Run()
        {
            GameObject root = null;
            IEnumerator polling = null;
            int exit = 1;
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-observerTestEndpoint");
                if (index < 0 || index + 1 >= args.Length)
                    throw new ArgumentException("Missing loopback fixture endpoint.");
                var endpoint = new Uri(args[index + 1]);
                if (!endpoint.IsLoopback || endpoint.Scheme != "http")
                    throw new ArgumentException("Network checks require a loopback HTTP fixture.");
                string origin = endpoint.AbsoluteUri.TrimEnd('/');
                root = new GameObject("Observer network checks");
                var controller = root.AddComponent<ObserverController>();
                controller.enabled = false;
                controller.LoadDemo();
                polling = (IEnumerator)typeof(ObserverController).GetMethod("Poll", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

                Connect(controller, origin + "/sequence");
                foreach (int tick in new[] { 10, 10, 10, 11, 11, 11, 12 })
                {
                    await PollOnce(polling);
                    Require(controller.LiveBuffer.Current.tick == tick, "Sequence rolled back or lost its last valid frame.");
                    if (tick == 11)
                        Require(controller.LiveBuffer.History.Count == 2, "Duplicate/invalid frame entered history.");
                }
                Require(controller.Status.StartsWith("Live /"), "Connection did not recover after HTTP/JSON errors.");
                Debug.Log("OBSERVER_NETWORK_SEQUENCE_PASSED");

                Connect(controller, origin + "/slow");
                var slow = BeginPoll(polling);
                controller.TogglePlayback();
                Require(controller.Hold, "Live display did not pause.");
                controller.TogglePlayback();
                await FinishPoll(polling, slow);
                Require(controller.LiveBuffer.Current == null, "Pre-pause response updated resumed display.");
                await PollOnce(polling);
                Require(controller.LiveBuffer.Current.tick == 30, "Display did not resume polling.");
                Debug.Log("OBSERVER_NETWORK_HOLD_PASSED");

                Connect(controller, origin + "/slow-change");
                slow = BeginPoll(polling);
                controller.ConnectionSettings(origin + "/fresh", "observer-rotated-fixture");
                controller.ToggleConnection();
                await FinishPoll(polling, slow);
                Require(controller.LiveBuffer.Current == null, "Old endpoint response entered the new connection.");
                await PollOnce(polling);
                Require(controller.LiveBuffer.Current.tick == 20, "New endpoint/token was not applied.");
                Debug.Log("OBSERVER_NETWORK_RECONNECT_PASSED");

                Connect(controller, origin + "/redirect");
                await PollOnce(polling);
                Require(controller.LiveBuffer.Current == null && controller.Status.StartsWith("Disconnected:"), "Redirect was accepted.");
                Connect(controller, origin + "/unauthorized");
                await PollOnce(polling);
                Require(controller.LiveBuffer.Current == null && controller.Status.Contains("401"), "Unauthorized response was accepted.");
                Connect(controller, origin + "/timeout");
                await PollOnce(polling);
                Require(controller.LiveBuffer.Current == null && controller.Status.StartsWith("Disconnected:"), "Timeout was not diagnosed.");
                Debug.Log("OBSERVER_NETWORK_ERRORS_PASSED");

                Connect(controller, origin + "/slow-replay");
                slow = BeginPoll(polling);
                string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "observer-network-" + Guid.NewGuid() + ".json");
                try
                {
                    System.IO.File.WriteAllText(path, JsonUtility.ToJson(new ObserverRecording
                    {
                        type = "observer-recording", replayVersion = 1, ticksPerSecond = 10,
                        frames = new[] { controller.Playback.Frames[0] }
                    }));
                    controller.LoadReplay(path);
                    await FinishPoll(polling, slow);
                    Require(!controller.IsLive && controller.Frame.tick == 0 && controller.Status == "Replay loaded", "Late request replaced an offline replay.");
                }
                finally { System.IO.File.Delete(path); }
                Debug.Log("OBSERVER_NETWORK_REPLAY_PASSED");

                Connect(controller, origin + "/slow-dispose");
                slow = BeginPoll(polling);
                ((IDisposable)polling).Dispose();
                polling = null;
                Require(typeof(ObserverController).GetField("pendingRequest", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller) == null,
                    "Disposed polling retained a request handle.");
                Debug.Log("OBSERVER_NETWORK_DISPOSE_PASSED");
                Debug.Log("OBSERVER_NETWORK_CHECKS_PASSED");
                exit = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                if (polling != null)
                    ((IDisposable)polling).Dispose();
                if (root != null)
                    UnityEngine.Object.DestroyImmediate(root);
                EditorApplication.Exit(exit);
            }
        }

        private static void Connect(ObserverController controller, string endpoint)
        {
            controller.ConnectionSettings(endpoint, "observer-network-fixture");
            controller.ToggleConnection();
            Require(controller.IsLive, "Fixture connection was not enabled.");
        }
        private static UnityWebRequestAsyncOperation BeginPoll(IEnumerator polling)
        {
            if (!polling.MoveNext() || !(polling.Current is UnityWebRequestAsyncOperation))
                throw new Exception("Polling did not start an HTTP request.");
            return (UnityWebRequestAsyncOperation)polling.Current;
        }
        private static async Task FinishPoll(IEnumerator polling, UnityWebRequestAsyncOperation operation)
        {
            while (!operation.isDone)
                await Task.Delay(20);
            Require(polling.MoveNext(), "Polling iterator stopped unexpectedly.");
        }
        private static Task PollOnce(IEnumerator polling) { return FinishPoll(polling, BeginPoll(polling)); }
        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new Exception(message);
        }
    }
}
