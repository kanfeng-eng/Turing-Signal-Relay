using UnityEngine;

namespace TuringSignalRelay
{
    public sealed class BootstrapController : MonoBehaviour
    {
        private MatchEngine engine;
        private void Start()
        {
            if (Camera.main == null)
            {
                var camera = new GameObject("BootstrapCamera").AddComponent<Camera>();
                camera.tag = "MainCamera";
                camera.orthographic = true;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.08f, 0.1f, 0.14f);
            }
            engine = new MatchEngine(new MatchConfig());
        }

        private void Update()
        {
            engine.Advance(Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (engine == null) return;
            GUI.Box(new Rect(10, 10, 520, 240), "Turing Signal Relay - Member 1 Bootstrap");
            GUILayout.BeginArea(new Rect(30, 45, 470, 200));
            GUILayout.Label("Development clock only. No gameplay or AI is implemented.");
            GUILayout.Label("Tick: " + engine.State.tick + " / " + engine.Clock.DurationTicks);
            GUILayout.Label("State: " + (engine.Clock.Completed ? "Completed" : engine.Clock.Paused ? "Paused" : "Running"));
            if (GUILayout.Button(engine.Clock.Paused ? "Resume" : "Pause"))
                engine.Clock.Paused = !engine.Clock.Paused;
            GUI.enabled = engine.Clock.Paused && !engine.Clock.Completed;
            if (GUILayout.Button("Step one tick")) engine.Step();
            GUI.enabled = true;
            if (GUILayout.Button("Restart")) engine = new MatchEngine(new MatchConfig());
            GUILayout.EndArea();
        }
    }
}
