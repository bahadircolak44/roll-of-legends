using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace RollOfLegends
{
    /// <summary>Little feedback effects: floating "+1 ATK" style texts and particle bursts.</summary>
    public class FxSpawner : MonoBehaviour
    {
        [Tooltip("Full-screen UI layer the floating texts are placed on (under the HUD).")]
        [SerializeField] private RectTransform _popupLayer;

        [SerializeField] private ParticleSystem _burstPrefab;

        [SerializeField] private float _popupSeconds = 1.6f;
        [SerializeField] private float _popupRise = 120f;

        private Font _font;
        private int _activePopups;

        /// <summary>Shows a text that rises and fades above a point in the world.</summary>
        public void Popup(Vector3 worldPosition, string text, Color color)
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var popup = new GameObject("Popup", typeof(RectTransform), typeof(Text), typeof(Outline));
            popup.transform.SetParent(_popupLayer, false);
            var label = popup.GetComponent<Text>();
            label.text = text;
            label.font = _font;
            label.fontSize = 52;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.color = color;
            var outline = popup.GetComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(3f, -3f);

            // Stack popups that appear at the same time so they don't overlap.
            float stackOffset = _activePopups * 56f;
            StartCoroutine(Animate((RectTransform)popup.transform, label, worldPosition, stackOffset));
        }

        public void Burst(Vector3 worldPosition, Color color)
        {
            if (_burstPrefab == null) return;
            ParticleSystem burst = Instantiate(_burstPrefab, worldPosition, Quaternion.identity, transform);
            ParticleSystem.MainModule main = burst.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color, Color.Lerp(color, Color.white, 0.5f));
            burst.Play();
            Destroy(burst.gameObject, 2f);
        }

        private IEnumerator Animate(RectTransform popup, Text label, Vector3 worldPosition, float stackOffset)
        {
            _activePopups++;
            Color color = label.color;
            for (float t = 0f; t < 1f; t += Time.deltaTime / _popupSeconds)
            {
                Vector2 local = WorldToLayer(worldPosition);
                popup.anchoredPosition = local + Vector2.up * (stackOffset + _popupRise * t);
                popup.localScale = Vector3.one * (t < 0.12f ? Mathf.Lerp(1.6f, 1f, t / 0.12f) : 1f); // quick "pop"
                color.a = t < 0.65f ? 1f : 1f - (t - 0.65f) / 0.35f;
                label.color = color;
                yield return null;
            }
            _activePopups--;
            Destroy(popup.gameObject);
        }

        private Vector2 WorldToLayer(Vector3 worldPosition)
        {
            Camera camera = Camera.main;
            Canvas canvas = _popupLayer.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            Vector2 screen = camera.WorldToScreenPoint(worldPosition);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_popupLayer, screen, uiCamera, out Vector2 local);
            return local;
        }
    }
}
