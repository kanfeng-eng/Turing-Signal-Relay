using System;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TuringSignalRelay.Editor
{
    public static class ObserverInteractionChecks
    {
        public static void Run()
        {
            var root = new GameObject("Observer interaction checks");
            string replay = Path.Combine(Path.GetTempPath(), "observer-switch-" + Guid.NewGuid() + ".json");
            try
            {
                var controller = root.AddComponent<ObserverController>();
                controller.enabled = false;
                controller.LoadDemo();
                root.AddComponent<ObserverHud>().Initialize(controller);
                var canvas = root.GetComponentInChildren<Canvas>();
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                var canvasRect = (RectTransform)canvas.transform;
                var maps = root.GetComponentsInChildren<ObserverMap>();
                var map = Array.Find(maps, value => !value.Mini);
                var mini = Array.Find(maps, value => value.Mini);
                foreach (var size in new[] { new Vector2(960, 600), new Vector2(1024, 768), new Vector2(1280, 800), new Vector2(1920, 1080), new Vector2(2560, 1440) })
                {
                    canvasRect.sizeDelta = new Vector2(960, size.y * 960 / size.x);
                    Render(root);
                    foreach (var button in root.GetComponentsInChildren<Button>())
                    {
                        if (button.transform.parent != canvas.transform)
                            continue;
                        var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(canvasRect, (RectTransform)button.transform);
                        Require(bounds.min.x >= canvasRect.rect.xMin - 1 && bounds.max.x <= canvasRect.rect.xMax + 1, "Toolbar clipped at " + size);
                    }
                    Require(((RectTransform)map.transform).rect.width > 0 && ((RectTransform)map.transform).rect.height > 0, "Map has no visible area.");
                    var panel = root.GetComponentInChildren<ScrollRect>();
                    float scrollRange = panel.content.rect.height - panel.viewport.rect.height;
                    panel.verticalNormalizedPosition = Mathf.Clamp01(1 - -((RectTransform)mini.transform).anchoredPosition.y / scrollRange);
                    Canvas.ForceUpdateCanvases();
                    var miniBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(panel.viewport, (RectTransform)mini.transform);
                    Require(miniBounds.min.y >= panel.viewport.rect.yMin - 1 && miniBounds.max.y <= panel.viewport.rect.yMax + 1, "Minimap cannot be reached by scrolling.");
                    panel.verticalNormalizedPosition = 1;
                }
                canvasRect.sizeDelta = new Vector2(960, 600);
                Render(root);
                var world = (RectTransform)map.transform.Find("World");
                var unit = (RectTransform)world.Find("Entity " + controller.Frame.entities[0].id);
                var input = new PointerEventData(root.GetComponentInChildren<EventSystem>());
                input.position = RectTransformUtility.WorldToScreenPoint(null, unit.position);
                map.OnPointerClick(input);
                Require(map.Selected == controller.Frame.entities[0].id, "Click did not select the nearest unit.");
                Render(root);
                Require(world.Find("Selected vision").gameObject.activeSelf, "Selected unit vision ring was not displayed.");
                map.Follow = true;
                controller.Playback.Seek(100);
                Render(root);
                unit = (RectTransform)world.Find("Entity " + map.Selected);
                Require((world.anchoredPosition + unit.anchoredPosition).magnitude < .1f, "Selected unit is not centered while following.");
                var before = world.anchoredPosition;
                input.position = RectTransformUtility.WorldToScreenPoint(null, map.transform.position) + new Vector2(40, 20);
                input.delta = new Vector2(40, 20);
                map.OnDrag(input);
                Render(root);
                Require(!map.Follow && (world.anchoredPosition - before).magnitude > 1, "Dragging did not pan or cancel follow.");
                float width = ((RectTransform)world.Find("Terrain")).rect.width;
                input.scrollDelta = new Vector2(0, 1);
                map.OnScroll(input);
                Render(root);
                Require(((RectTransform)world.Find("Terrain")).rect.width > width, "Scroll did not zoom.");
                var miniRect = (RectTransform)mini.transform;
                Vector2 miniCenter = RectTransformUtility.WorldToScreenPoint(null, miniRect.TransformPoint(miniRect.rect.center));
                input.position = miniCenter;
                mini.OnPointerClick(input);
                Render(root);
                Require(world.anchoredPosition.magnitude < .1f, "Minimap center did not center the main map.");
                input.position = miniCenter + new Vector2(25, 0);
                mini.OnPointerClick(input);
                Render(root);
                Require(world.anchoredPosition.magnitude > 1, "Minimap did not reposition the main map.");
                map.ResetCamera();
                Render(root);
                Require(world.anchoredPosition.magnitude < .1f, "Reset did not center the map.");
                controller.SelectSide(0);
                Render(root);
                foreach (var entity in controller.Frame.entities)
                {
                    var view = world.Find("Entity " + entity.id);
                    if (!controller.Frame.Visible(entity, 0) && view != null)
                        Require(!view.gameObject.activeSelf, "Hidden entity remained visible.");
                }
                controller.SelectSide(-1);
                controller.Frame.eventFocusId = controller.Frame.entities[0].id;
                map.FollowEvents = true;
                Render(root);
                Require(map.Selected == controller.Frame.eventFocusId && map.Follow, "Event follow did not select its target.");
                var replacement = JsonUtility.FromJson<ObserverFrame>(JsonUtility.ToJson(controller.Frame));
                replacement.tick = 0;
                replacement.entities[0].id = 100000;
                replacement.links = new ObserverLink[0];
                replacement.eventFocusId = -1;
                File.WriteAllText(replay, JsonUtility.ToJson(new ObserverRecording { type = "observer-recording", replayVersion = 1, ticksPerSecond = 10, frames = new[] { replacement } }));
                controller.LoadReplay(replay);
                Render(root);
                Require(map.Selected == -1 && !map.Follow && !map.FollowEvents, "Previous replay selection/follow leaked into a new recording.");
                Require(world.Find("Entity 100000") != null, "Replacement entity did not render.");
                Require(world.Find("Entity " + controller.Playback.Frames[0].eventFocusId) == null, "Stale selection sentinel was rendered.");
                int entities = 0;
                foreach (Transform child in world)
                    if (child.name.StartsWith("Entity ")) entities++;
                Require(entities == replacement.entities.Length, "Previous replay retained stale entity views.");
                foreach (var field in root.GetComponentsInChildren<InputField>())
                {
                    if (field.contentType == InputField.ContentType.Password)
                    {
                        field.text = "observer-test-token";
                        Require(!field.textComponent.text.Contains("observer-test-token"), "Token was displayed as plain text.");
                    }
                }
                foreach (string address in new[] { "http://111.231.101.147/api/matches/example/snapshot", "https://example.com/snapshot" })
                {
                    controller.ConnectionSettings(address, "observer-test-token");
                    controller.ToggleConnection();
                    Require(controller.IsLive, "HTTP/HTTPS snapshot URL was rejected.");
                    controller.ToggleConnection();
                }
                controller.ConnectionSettings("file:///tmp/snapshot.json", "");
                controller.ToggleConnection();
                Require(!controller.IsLive && controller.Error.Length > 0, "Non-HTTP snapshot URL was accepted.");
                Debug.Log("OBSERVER_INTERACTION_CHECKS_PASSED sizes=5 selection drag zoom minimap follow visibility replay-reset token http-https");
            }
            finally
            {
                File.Delete(replay);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        internal static void Render(GameObject root)
        {
            Canvas.ForceUpdateCanvases();
            var hud = root.GetComponent<ObserverHud>();
            if (hud != null) typeof(ObserverHud).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
            foreach (var map in root.GetComponentsInChildren<ObserverMap>())
                typeof(ObserverMap).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(map, null);
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
