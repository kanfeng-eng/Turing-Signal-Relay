using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TuringSignalRelay
{
    public sealed class ObserverHud : MonoBehaviour
    {
        private ObserverController controller;
        private ObserverMap map;
        private ObserverGraph graph;
        private Text data, status, playLabel, connectLabel, selection;
        private InputField path, url, token;
        private Slider timeline;
        private Button step;
        private Font font;
        private ObserverFrame graphedFrame;
        private readonly Color panelColor = new Color(.10f, .20f, .27f);
        private readonly Color textColor = new Color(.88f, .94f, .96f);

        public void Initialize(ObserverController source)
        {
            controller = source;
            font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Segoe UI", "Arial" }, 16);
            var canvasObject = new GameObject("ObserverCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 800);
            scaler.matchWidthOrHeight = .5f;
            RectTransform root = (RectTransform)canvasObject.transform;
            root.gameObject.AddComponent<Image>().color = new Color(.055f, .105f, .15f);
            if (FindFirstObjectByType<EventSystem>() == null)
                new GameObject("ObserverInput", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform, false);
            var heading = Label(root, "电波接力   /   观战台", 16, 14, 800, 30, 22);
            heading.color = new Color(.52f, .82f, .81f);
            float x = 16;
            playLabel = Button(root, "Play", x, 54, 90, () => controller.TogglePlayback()).GetComponentInChildren<Text>();
            x += 98;
            step = Button(root, "Step +1", x, 54, 90, () => controller.Playback.Step());
            x += 98;
            foreach (float speed in new[] { .5f, 1, 2, 4 })
            {
                float value = speed;
                Button(root, value + "x", x, 54, 48, () => controller.Playback.Speed = value);
                x += 54;
            }
            Button(root, "All", x, 54, 70, () => controller.SelectSide(-1));
            x += 78;
            Button(root, "Red view", x, 54, 94, () => controller.SelectSide(0));
            x += 102;
            Button(root, "Blue view", x, 54, 94, () => controller.SelectSide(1));
            x += 102;
            Button(root, "Reset camera", x, 54, 130, () => map.ResetCamera());
            timeline = Rect(root, "Timeline", 16, 98, 900, 18).gameObject.AddComponent<Slider>();
            StretchHorizontal(timeline.GetComponent<RectTransform>(), 16, 16);
            var rail = Rect(timeline.transform, "Rail", 0, 5, 900, 8).gameObject.AddComponent<Image>();
            rail.color = panelColor;
            StretchHorizontal(rail.rectTransform, 0, 0);
            var handle = Rect(timeline.transform, "Handle", 0, 0, 14, 18).gameObject.AddComponent<Image>();
            handle.color = new Color(.52f, .82f, .81f);
            timeline.handleRect = handle.rectTransform;
            timeline.targetGraphic = handle;
            timeline.wholeNumbers = true;
            timeline.onValueChanged.AddListener(value => { if (!controller.IsLive) controller.Playback.Seek((int)value); });
            status = Label(root, "", 16, 119, 1200, 23, 12);
            var mapArea = Rect(root, "Map", 16, 150, 900, 630);
            mapArea.anchorMin = new Vector2(0, 0);
            mapArea.anchorMax = new Vector2(1, 1);
            mapArea.offsetMin = new Vector2(16, 20);
            mapArea.offsetMax = new Vector2(-360, -150);
            mapArea.gameObject.AddComponent<Image>().color = new Color(.04f, .075f, .1f);
            mapArea.gameObject.AddComponent<RectMask2D>();
            map = mapArea.gameObject.AddComponent<ObserverMap>();
            map.Initialize(controller, font);
            var sidebar = Rect(root, "Data panel", 934, 150, 330, 630);
            sidebar.anchorMin = new Vector2(1, 0);
            sidebar.anchorMax = new Vector2(1, 1);
            sidebar.offsetMin = new Vector2(-344, 20);
            sidebar.offsetMax = new Vector2(-16, -150);
            sidebar.gameObject.AddComponent<Image>().color = panelColor;
            var viewport = Rect(sidebar, "Viewport", 12, 12, 304, 600);
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(12, 12);
            viewport.offsetMax = new Vector2(-12, -12);
            viewport.gameObject.AddComponent<Image>().color = panelColor;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect(viewport, "Content", 0, 0, 300, 920);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.sizeDelta = new Vector2(0, 920);
            var scroll = sidebar.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport;
            scroll.content = content;
            scroll.horizontal = false;
            scroll.scrollSensitivity = 30;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            data = Label(content, "", 0, 0, 300, 170, 15);
            Button(content, "Follow selected", 0, 180, 145, () => map.Follow = !map.Follow);
            Button(content, "Follow events", 153, 180, 145, () => map.FollowEvents = !map.FollowEvents);
            selection = Label(content, "Click an entity to inspect", 0, 222, 300, 46, 13);
            Label(content, "Replay file (.json / .jsonl)", 0, 278, 300, 24, 13);
            path = Input(content, "Replay path", 0, 306, false);
            Button(content, "Open replay", 0, 346, 298, () => controller.LoadReplay(path.text));
            Label(content, "Live snapshot URL", 0, 395, 300, 22, 13);
            url = Input(content, "Snapshot URL", 0, 421, false);
            Label(content, "Access token", 0, 465, 300, 22, 13);
            token = Input(content, "Access token", 0, 491, true);
            url.onEndEdit.AddListener(_ => controller.ConnectionSettings(url.text, token.text));
            token.onEndEdit.AddListener(_ => controller.ConnectionSettings(url.text, token.text));
            connectLabel = Button(content, "Connect", 0, 531, 298, () => { controller.ConnectionSettings(url.text, token.text); controller.ToggleConnection(); }).GetComponentInChildren<Text>();
            Label(content, "R / B sides   |   C courier / E engineer / T transporter\nScroll map to zoom; drag to pan. Panel also scrolls.", 0, 580, 300, 62, 12);
            Label(content, "Score history", 0, 650, 300, 22, 13);
            graph = Rect(content, "Score history", 0, 677, 298, 70).gameObject.AddComponent<ObserverGraph>();
            graph.raycastTarget = false;
            var mini = Rect(content, "Minimap", 0, 769, 298, 140);
            mini.gameObject.AddComponent<Image>().color = new Color(.04f, .075f, .1f);
            mini.gameObject.AddComponent<RectMask2D>();
            var minimap = mini.gameObject.AddComponent<ObserverMap>();
            minimap.Mini = true;
            minimap.MainMap = map;
            minimap.Initialize(controller, font);
        }
        private static void StretchHorizontal(RectTransform rect, float left, float right)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(left, rect.offsetMin.y);
            rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
        }
        private static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        private Text Label(Transform parent, string value, float x, float y, float width, float height, int size)
        {
            var text = Rect(parent, value.Length > 32 ? value.Substring(0, 32) : value, x, y, width, height).gameObject.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.color = textColor;
            text.text = Caption(value);
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        private static string Caption(string value)
        {
            switch (value)
            {
                case "Play":
                    return "播放";
                case "Step +1":
                    return "单步";
                case "All":
                    return "全图";
                case "Red view":
                    return "红方视角";
                case "Blue view":
                    return "蓝方视角";
                case "Reset camera":
                    return "重置镜头";
                case "Follow selected":
                    return "跟随选中单位";
                case "Follow events":
                    return "跟随事件";
                case "Open replay":
                    return "打开回放";
                case "Connect":
                    return "连接观战";
                case "Live snapshot URL":
                    return "观战快照地址";
                case "Access token":
                    return "访问令牌";
                case "Replay file (.json / .jsonl)":
                    return "回放文件路径 (.json / .jsonl)";
                case "Score history":
                    return "得分曲线";
                default:
                    return value;
            }
        }
        private Button Button(Transform parent, string caption, float x, float y, float width, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(parent, caption, x, y, width, 34);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = new Color(.18f, .35f, .44f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(.65f, .9f, .92f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            var text = Label(rect, caption, 4, 0, width - 8, 34, 14);
            text.alignment = TextAnchor.MiddleCenter;
            button.onClick.AddListener(action);
            return button;
        }
        private InputField Input(Transform parent, string caption, float x, float y, bool password)
        {
            var rect = Rect(parent, caption, x, y, 298, 32);
            rect.gameObject.AddComponent<Image>().color = new Color(.05f, .12f, .17f);
            var text = Label(rect, "", 7, 0, 284, 32, 14);
            text.alignment = TextAnchor.MiddleLeft;
            var field = rect.gameObject.AddComponent<InputField>();
            field.textComponent = text;
            if (password)
                field.contentType = InputField.ContentType.Password;
            return field;
        }
        private void Update()
        {
            if (controller == null || data == null || controller.Playback.Frames.Count == 0)
                return;
            var frame = controller.Frame;
            int ticksPerSecond = controller.IsLive ? 10 : controller.Playback.TicksPerSecond;
            data.text = "Tick " + frame.tick + "  /  " + ticksPerSecond + "Hz\n" +
                "RED  " + frame.scoreHalfPoints[0] / 2f + "     BLUE  " + frame.scoreHalfPoints[1] / 2f + "\n" +
                "Remaining  " + Math.Max(0, ((controller.IsLive ? frame.durationTicks : controller.Playback.Frames.Count - 1) - frame.tick) / (float)ticksPerSecond).ToString("0.0") + "s\n" +
                "Weather  " + frame.weather + "\nHub  " + Mathf.RoundToInt(frame.hubProgress * 100) + "%\n" +
                "Event  " + frame.eventText + "\nNext  " + (frame.nextEventTick < 0 ? "--" : Math.Max(0, (frame.nextEventTick - frame.tick) / (float)ticksPerSecond) + "s");
            status.text = controller.Status + "  /  " + frame.rulesVersion + "   " + controller.Error;
            playLabel.text = controller.IsLive ? (controller.Hold ? "回到实时" : "暂停显示") : (controller.Playback.Playing ? "暂停" : "播放");
            connectLabel.text = controller.IsLive ? "断开连接" : "连接观战";
            step.interactable = !controller.IsLive;
            timeline.interactable = !controller.IsLive;
            timeline.maxValue = controller.Playback.Frames.Count - 1;
            timeline.SetValueWithoutNotify(controller.Playback.Index);
            selection.text = "Select entity / follow controls";
            foreach (var entity in frame.entities)
            if (entity.id == map.Selected && frame.Visible(entity, controller.Side))
                selection.text = entity.label + "\n" + entity.x.ToString("0.0") + ", " + entity.y.ToString("0.0") + (entity.suppressed ? "  Suppressed" : "");
            if (frame != graphedFrame)
            {
                graph.Frames = controller.IsLive ? controller.LiveBuffer.History : controller.Playback.Frames;
                graph.Last = controller.IsLive ? graph.Frames.Count - 1 : controller.Playback.Index;
                graph.SetVerticesDirty();
                graphedFrame = frame;
            }
        }
    }
}
