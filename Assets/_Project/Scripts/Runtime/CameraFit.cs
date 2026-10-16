using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// Keeps the camera's angle fixed, but moves it so the whole board is as big as possible
    /// in the screen space that the UI (stats, history, cards) leaves free. Works for portrait,
    /// landscape and any Editor Game view size. To change the viewing angle, rotate the camera.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFit : MonoBehaviour
    {
        [SerializeField] private Board _board;

        [Tooltip("The UI layout whose panels the board must not be hidden behind.")]
        [SerializeField] private ScreenLayout _layout;

        [Tooltip("Extra empty space around the board, in world units.")]
        [SerializeField] private float _padding = 0.15f;

        [Tooltip("Extra gap between the board and the UI, as a fraction of the screen.")]
        [SerializeField] private float _uiGap = 0.01f;

        private Camera _camera;
        private Vector2Int _fittedSize;
        private float _fittedAspect;

        /// <summary>The part of the screen (0..1 viewport units) the board was fitted into.</summary>
        public Rect SafeArea { get; private set; } = new Rect(0, 0, 1, 1);

        private Camera Camera => _camera != null ? _camera : (_camera = GetComponent<Camera>());

        private void LateUpdate()
        {
            var size = new Vector2Int(Camera.pixelWidth, Camera.pixelHeight);
            if (size != _fittedSize || !Mathf.Approximately(Camera.aspect, _fittedAspect))
            {
                Fit();
            }
        }

        public void Fit()
        {
            _fittedSize = new Vector2Int(Camera.pixelWidth, Camera.pixelHeight);
            _fittedAspect = Camera.aspect;
            SafeArea = ComputeSafeArea();

            Bounds bounds = GetBoardBounds();
            bounds.Expand(_padding * 2f);

            // Board corners as seen from the camera's angle (x = right, y = up, z = forward).
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                corners[i] = transform.InverseTransformDirection(corner - bounds.center);
            }

            // Find the closest distance where every corner fits inside the safe area.
            // Moving the camera back only ever makes things fit better, so a binary search works.
            float near = 0.1f;
            float far = 1000f;
            for (int i = 0; i < 40; i++)
            {
                float middle = (near + far) / 2f;
                if (TryFit(corners, middle, out _)) far = middle;
                else near = middle;
            }

            TryFit(corners, far, out Vector2 shift);
            // Keeping the angle fixed: move back by the distance, then slide sideways/up to center in the safe area.
            transform.position = bounds.center - transform.forward * far - transform.right * shift.x - transform.up * shift.y;
        }

        /// <summary>
        /// Checks if all corners fit when the camera is <paramref name="distance"/> away from the board center.
        /// <paramref name="shift"/> is how far to slide the camera so the board sits in the middle of the safe area.
        /// </summary>
        private bool TryFit(Vector3[] corners, float distance, out Vector2 shift)
        {
            float tanVertical = Mathf.Tan(Camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanHorizontal = tanVertical * Camera.aspect;
            // Safe area edges expressed as slopes (screen position per unit of depth).
            float left = tanHorizontal * (2f * SafeArea.xMin - 1f);
            float right = tanHorizontal * (2f * SafeArea.xMax - 1f);
            float bottom = tanVertical * (2f * SafeArea.yMin - 1f);
            float top = tanVertical * (2f * SafeArea.yMax - 1f);

            Vector2 lowest = new Vector2(float.MinValue, float.MinValue);
            Vector2 highest = new Vector2(float.MaxValue, float.MaxValue);
            foreach (Vector3 corner in corners)
            {
                float depth = corner.z + distance;
                if (depth <= 0.01f)
                {
                    shift = Vector2.zero;
                    return false;
                }
                lowest = Vector2.Max(lowest, new Vector2(left * depth - corner.x, bottom * depth - corner.y));
                highest = Vector2.Min(highest, new Vector2(right * depth - corner.x, top * depth - corner.y));
            }

            shift = (lowest + highest) / 2f;
            return lowest.x <= highest.x && lowest.y <= highest.y;
        }

        /// <summary>The screen minus the UI panels: bars along an edge push that edge inward.</summary>
        private Rect ComputeSafeArea()
        {
            float left = 0f, right = 1f, bottom = 0f, top = 1f;
            if (_layout == null) return new Rect(0, 0, 1, 1);

            Canvas.ForceUpdateCanvases();
            foreach (RectTransform panel in _layout.Obstacles)
            {
                if (panel == null || !panel.gameObject.activeInHierarchy) continue;
                Rect area = ViewportRect(panel);
                bool isHorizontalBar = area.width * Camera.pixelWidth >= area.height * Camera.pixelHeight;
                if (isHorizontalBar)
                {
                    if (area.center.y > 0.5f) top = Mathf.Min(top, area.yMin - _uiGap);
                    else bottom = Mathf.Max(bottom, area.yMax + _uiGap);
                }
                else
                {
                    if (area.center.x < 0.5f) left = Mathf.Max(left, area.xMax + _uiGap);
                    else right = Mathf.Min(right, area.xMin - _uiGap);
                }
            }
            return Rect.MinMaxRect(left, bottom, right, top);
        }

        private Rect ViewportRect(RectTransform panel)
        {
            Canvas canvas = panel.GetComponentInParent<Canvas>().rootCanvas;
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            var corners = new Vector3[4];
            panel.GetWorldCorners(corners);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);
            var size = new Vector2(Camera.pixelWidth, Camera.pixelHeight);
            return Rect.MinMaxRect(min.x / size.x, min.y / size.y, max.x / size.x, max.y / size.y);
        }

        private Bounds GetBoardBounds()
        {
            Renderer[] renderers = _board.GetComponentsInChildren<Renderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer tileRenderer in renderers)
            {
                bounds.Encapsulate(tileRenderer.bounds);
            }
            return bounds;
        }
    }
}
