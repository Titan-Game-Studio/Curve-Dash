using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DevionGames.InventorySystem;
using DevionGames.UIWidgets;

namespace STG.CurveDash
{
    /// <summary>
    /// Draws a Path-of-Exile-style radial cooldown sweep (plus a small seconds countdown) over each filled
    /// slot of the Devion "Actionbar" container. The sweep is driven by <see cref="PlayerCooldownTracker"/>,
    /// i.e. the player's attack cadence — which in this auto-attack model is exactly how often the socketed
    /// active skills fire. Self-instantiates at runtime so it needs no scene wiring, and decorates Devion's
    /// runtime slots non-destructively (overlay GameObjects are added as children and reused).
    /// </summary>
    public class ActionbarCooldownUI : MonoBehaviour
    {
        private const string ContainerName = "Actionbar";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindAnyObjectByType<ActionbarCooldownUI>() != null) return;
            var go = new GameObject("[ActionbarCooldownUI]");
            DontDestroyOnLoad(go);
            go.AddComponent<ActionbarCooldownUI>();
        }

        private sealed class Overlay
        {
            public Image Radial;
            public Text Countdown;
        }

        private Font _font;
        private Sprite _whiteSprite;
        private readonly Dictionary<Slot, Overlay> _overlays = new Dictionary<Slot, Overlay>();
        private readonly List<Slot> _stale = new List<Slot>();

        private void Awake()
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");

            // A 4x4 white sprite radially filled gives the classic square "pie wipe" over a slot icon.
            var tex = Texture2D.whiteTexture;
            _whiteSprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        private void LateUpdate()
        {
            float fraction = PlayerCooldownTracker.Fraction;     // 1 = just fired, 0 = ready
            float remaining = PlayerCooldownTracker.Remaining;

            var containers = WidgetUtility.FindAll<ItemContainer>(ContainerName);
            if (containers == null || containers.Length == 0) return;

            var seen = new HashSet<Slot>();
            foreach (var container in containers)
            {
                if (container == null) continue;
                foreach (var slot in container.Slots)
                {
                    if (slot == null) continue;
                    seen.Add(slot);

                    bool hasSkill = !slot.IsEmpty && slot.ObservedItem != null;
                    bool show = hasSkill && fraction > 0f;

                    var overlay = GetOrCreateOverlay(slot);
                    overlay.Radial.gameObject.SetActive(show);
                    overlay.Countdown.gameObject.SetActive(show);

                    if (show)
                    {
                        overlay.Radial.fillAmount = fraction;
                        overlay.Countdown.text = remaining >= 1f
                            ? remaining.ToString("0")
                            : remaining.ToString("0.0");
                    }
                }
            }

            // Drop overlays whose slot was destroyed/rebuilt (Devion can recreate slots on container changes).
            _stale.Clear();
            foreach (var kvp in _overlays)
                if (kvp.Key == null || !seen.Contains(kvp.Key)) _stale.Add(kvp.Key);
            foreach (var s in _stale) _overlays.Remove(s);
        }

        private Overlay GetOrCreateOverlay(Slot slot)
        {
            if (_overlays.TryGetValue(slot, out var existing) && existing.Radial != null)
                return existing;

            // Radial dark sweep stretched over the whole slot, drawn above the icon.
            var radialGO = new GameObject("CooldownRadial", typeof(RectTransform), typeof(Image));
            radialGO.transform.SetParent(slot.transform, false);
            var rrt = (RectTransform)radialGO.transform;
            rrt.anchorMin = Vector2.zero; rrt.anchorMax = Vector2.one;
            rrt.offsetMin = Vector2.zero; rrt.offsetMax = Vector2.zero;
            var radial = radialGO.GetComponent<Image>();
            radial.sprite = _whiteSprite;
            radial.type = Image.Type.Filled;
            radial.fillMethod = Image.FillMethod.Radial360;
            radial.fillOrigin = (int)Image.Origin360.Top;
            radial.fillClockwise = true;
            radial.color = new Color(0f, 0f, 0f, 0.62f);
            radial.raycastTarget = false;
            radialGO.transform.SetAsLastSibling();

            // Seconds countdown centred on top of the sweep.
            var textGO = new GameObject("CooldownText", typeof(RectTransform), typeof(Text));
            textGO.transform.SetParent(slot.transform, false);
            var trt = (RectTransform)textGO.transform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero; trt.offsetMax = Vector2.zero;
            var text = textGO.GetComponent<Text>();
            text.font = _font;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 8;
            text.resizeTextMaxSize = 28;
            text.raycastTarget = false;
            text.supportRichText = false;
            textGO.transform.SetAsLastSibling();

            var overlay = new Overlay { Radial = radial, Countdown = text };
            _overlays[slot] = overlay;
            return overlay;
        }
    }
}
