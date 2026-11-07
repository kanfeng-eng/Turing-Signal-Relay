using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TuringSignalRelay
{
    public sealed class ObserverMap : MonoBehaviour, IScrollHandler, IDragHandler, IPointerClickHandler
    {
        private ObserverController controller;
        private RectTransform viewport, world;
        private RectTransform rangeRing;
        private RawImage ground;
        private Texture2D terrainTexture;
        private int terrainHash;
        private readonly Dictionary<int, RectTransform> units = new Dictionary<int, RectTransform>();
        private readonly List<RectTransform> links = new List<RectTransform>();
        private float zoom = 1;
        private Vector2 pan;
        public int Selected = -1;
        public bool Follow, FollowEvents;
        public bool Mini;
        public ObserverMap MainMap;
        private int eventTick = -1;
        private Font font;
        private readonly Sprite[] sprites = new Sprite[5];
        private int sourceVersion = -1;
        private static readonly string[] KindLabels = { "C", "E", "T", "M", "S" };

        public void Initialize(ObserverController source, Font labelFont)
        {
            controller = source;
            font = labelFont;
            viewport = (RectTransform)transform;
            string[] names = { "police", "crime", "student", "book", "wall1" };
            for (int i = 0; i < names.Length; i++)
                sprites[i] = Resources.Load<Sprite>("LegacySprites/" + names[i]);
            world = NewRect("World", viewport);
            world.anchorMin = world.anchorMax = world.pivot = new Vector2(.5f, .5f);
            ground = NewRect("Terrain", world).gameObject.AddComponent<RawImage>();
            ground.raycastTarget = false;
            rangeRing = NewRect("Selected vision", world);
            rangeRing.anchorMin = rangeRing.anchorMax = rangeRing.pivot = new Vector2(.5f, .5f);
            var ring = rangeRing.gameObject.AddComponent<ObserverRangeRing>();
            ring.raycastTarget = false;
            ring.color = new Color(.9f, .85f, .4f, .8f);
            rangeRing.gameObject.SetActive(false);
        }
        private static RectTransform NewRect(string name, Transform parent)
        {
            var result = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            result.SetParent(parent, false);
            return result;
        }
        private void LateUpdate()
        {
            if (controller == null || controller.Playback.Frames.Count == 0)
                return;
            if (sourceVersion != controller.SourceVersion)
            {
                foreach (var pair in units)
                    Release(pair.Value.gameObject);
                units.Clear();
                foreach (var line in links)
                    Release(line.gameObject);
                links.Clear();
                Selected = -1;
                FollowEvents = false;
                eventTick = -1;
                ResetCamera();
                sourceVersion = controller.SourceVersion;
            }
            var frame = controller.Frame;
            int hash = (controller.Side * 101 + frame.width) * 101 + frame.height;
            hash = hash * 2 + (frame.staticTerrainKnown ? 1 : 0);
            for (int i = 0; i < frame.terrain.Length; i++)
                hash = unchecked(hash * 31 + frame.terrain[i] * 7 + frame.visibleToMask[i]);
            if (terrainTexture == null || hash != terrainHash)
            {
                if (terrainTexture != null)
                    Release(terrainTexture);
                terrainTexture = new Texture2D(frame.width, frame.height, TextureFormat.RGBA32, false);
                terrainTexture.filterMode = FilterMode.Point;
                Color[] palette = { new Color(.16f, .29f, .24f), new Color(.55f, .52f, .39f), new Color(.16f, .38f, .55f), new Color(.37f, .4f, .39f) };
                var pixels = new Color[frame.terrain.Length];
                for (int i = 0; i < pixels.Length; i++)
                    pixels[i] = !frame.staticTerrainKnown && controller.Side >= 0 && (frame.visibleToMask[i] & (1 << controller.Side)) == 0 ? new Color(.05f, .08f, .11f) : palette[frame.terrain[i]];
                terrainTexture.SetPixels(pixels);
                terrainTexture.Apply();
                ground.texture = terrainTexture;
                terrainHash = hash;
            }
            if (FollowEvents && frame.tick != eventTick && frame.eventFocusId >= 0)
            {
                Selected = frame.eventFocusId;
                Follow = true;
                eventTick = frame.tick;
            }
            float scale = Math.Min(viewport.rect.width / frame.width, viewport.rect.height / frame.height) * (Mini ? 1 : zoom);
            if (Follow && !Mini)
                foreach (var entity in frame.entities)
                if (entity.id == Selected && frame.Visible(entity, controller.Side))
                    pan = -Position(entity, frame) * scale;
            world.anchoredPosition = Mini ? Vector2.zero : pan;
            ground.rectTransform.anchorMin = ground.rectTransform.anchorMax = ground.rectTransform.pivot = new Vector2(.5f, .5f);
            ground.rectTransform.sizeDelta = new Vector2(frame.width, frame.height) * scale;
            rangeRing.gameObject.SetActive(false);
            foreach (var entity in frame.entities)
                if (!Mini && entity.id == Selected && entity.kind <= 2 && frame.Visible(entity, controller.Side))
                {
                    int radius = entity.kind == 0 ? 5 : entity.kind == 1 ? 4 : 3;
                    rangeRing.gameObject.SetActive(true);
                    rangeRing.anchoredPosition = Position(entity, frame) * scale;
                    rangeRing.sizeDelta = Vector2.one * radius * 2 * scale;
                }
            foreach (var pair in units)
                pair.Value.gameObject.SetActive(false);
            foreach (var entity in frame.entities)
            {
                if (!frame.Visible(entity, controller.Side))
                    continue;
                RectTransform view;
                if (!units.TryGetValue(entity.id, out view))
                {
                    view = NewRect("Entity " + entity.id, world);
                    view.anchorMin = view.anchorMax = view.pivot = new Vector2(.5f, .5f);
                    view.gameObject.AddComponent<Image>().raycastTarget = false;
                    var icon = NewRect("Icon", view);
                    icon.anchorMin = Vector2.zero;
                    icon.anchorMax = Vector2.one;
                    icon.offsetMin = Vector2.one * 2;
                    icon.offsetMax = -Vector2.one * 2;
                    var image = icon.gameObject.AddComponent<Image>();
                    image.raycastTarget = false;
                    image.preserveAspect = true;
                    string[] art = { "police", "crime", "student", "book", "wall1" };
                    image.sprite = Resources.Load<Sprite>("LegacySprites/" + art[Mathf.Clamp(entity.kind, 0, 4)]);
                    image.enabled = image.sprite != null;
                    var label = NewRect("Label", view);
                    label.anchorMin = label.anchorMax = new Vector2(.5f, 0);
                    label.pivot = new Vector2(.5f, 1);
                    label.sizeDelta = new Vector2(100, 22);
                    var text = label.gameObject.AddComponent<Text>();
                    text.font = font;
                    text.fontSize = 11;
                    text.alignment = TextAnchor.UpperCenter;
                    text.raycastTarget = false;
                    units.Add(entity.id, view);
                }
                var currentIcon = view.GetChild(0).GetComponent<Image>();
                currentIcon.sprite = sprites[Mathf.Clamp(entity.kind, 0, 4)];
                currentIcon.enabled = currentIcon.sprite != null;
                view.gameObject.SetActive(true);
                view.anchoredPosition = Position(entity, frame) * scale;
                view.sizeDelta = Vector2.one * (Mini ? 5 : Math.Max(18, scale * 2.7f));
                view.GetComponent<Image>().color = entity.side < 0 ? new Color(.7f, .72f, .65f) : entity.side == 0 ? new Color(.88f, .4f, .35f) : new Color(.36f, .7f, .91f);
                var labelText = view.GetComponentInChildren<Text>();
                labelText.enabled = !Mini;
                labelText.text = (entity.side < 0 ? "·" : entity.side == 0 ? "R" : "B") + KindLabels[entity.kind] +
                    (zoom >= 2 || entity.id == Selected ? entity.id.ToString() : "") + (entity.suppressed ? " !" : "");
            }
            while (links.Count < frame.links.Length)
            {
                var line = NewRect("Link", world);
                line.anchorMin = line.anchorMax = line.pivot = new Vector2(.5f, .5f);
                line.gameObject.AddComponent<Image>().raycastTarget = false;
                line.SetSiblingIndex(1);
                links.Add(line);
            }
            for (int i = 0; i < links.Count; i++)
            {
                var line = links[i];
                line.gameObject.SetActive(false);
                if (i >= frame.links.Length)
                    continue;
                var link = frame.links[i];
                var from = Array.Find(frame.entities, e => e.id == link.from);
                var to = Array.Find(frame.entities, e => e.id == link.to);
                if (from == null || to == null || !frame.Visible(from, controller.Side) || !frame.Visible(to, controller.Side))
                    continue;
                Vector2 a = Position(from, frame) * scale, b = Position(to, frame) * scale;
                line.gameObject.SetActive(true);
                line.anchoredPosition = (a + b) / 2;
                line.sizeDelta = new Vector2((b - a).magnitude, Mini ? 1 : 2);
                line.localEulerAngles = new Vector3(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                line.GetComponent<Image>().color = link.connected ? new Color(.45f, .85f, .69f) : Color.gray;
            }
        }
        private static Vector2 Position(ObserverEntity entity, ObserverFrame frame)
        {
            return new Vector2(entity.x + .5f - frame.width / 2f, entity.y + .5f - frame.height / 2f);
        }
        public void ResetCamera()
        {
            zoom = 1;
            pan = Vector2.zero;
            Follow = false;
        }
        private static void Release(UnityEngine.Object value)
        {
            var gameObject = value as GameObject;
            if (gameObject != null)
                gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(value);
            else
                DestroyImmediate(value);
        }
        public void OnScroll(PointerEventData e)
        {
            if (!Mini)
                zoom = Mathf.Clamp(zoom * (1 + e.scrollDelta.y * .12f), .5f, 4);
        }
        public void OnDrag(PointerEventData e)
        {
            if (Mini)
                return;
            Follow = false;
            Vector2 local, previous;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, e.position, e.pressEventCamera, out local);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, e.position - e.delta, e.pressEventCamera, out previous);
            pan += local - previous;
        }
        public void OnPointerClick(PointerEventData e)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(viewport, e.position, e.pressEventCamera, out local);
            local -= viewport.rect.center;
            if (Mini && MainMap != null)
            {
                MainMap.Follow = false;
                MainMap.pan = -local * MainMap.zoom * Math.Min(MainMap.viewport.rect.width / controller.Frame.width, MainMap.viewport.rect.height / controller.Frame.height) / Math.Min(viewport.rect.width / controller.Frame.width, viewport.rect.height / controller.Frame.height);
                return;
            }
            float nearest = 24;
            Selected = -1;
            foreach (var pair in units)
                if (pair.Value.gameObject.activeSelf)
                {
                    float distance = (pair.Value.anchoredPosition + world.anchoredPosition - local).magnitude;
                    if (distance < nearest)
                    {
                        nearest = distance;
                        Selected = pair.Key;
                    }
                }
        }
        private void OnDestroy()
        {
            if (terrainTexture != null)
                Release(terrainTexture);
        }
    }
}
