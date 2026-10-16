using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// Keeps the camera's angle fixed, but moves it so the whole board fits on any screen shape
    /// (portrait phone, landscape phone, Editor Game view), between the UI at the top and the cards at the bottom.
    /// To change the viewing angle, rotate the camera in the Scene view.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFit : MonoBehaviour
    {
        [SerializeField] private Board _board;

        [Tooltip("Extra empty space around the board, in world units.")]
        [SerializeField] private float _padding = 0.3f;

        [Tooltip("Part of the screen height kept free at the top (for the info panel).")]
        [SerializeField, Range(0f, 0.4f)] private float _topMargin = 0.2f;

        [Tooltip("Part of the screen height kept free at the bottom (for the cards).")]
        [SerializeField, Range(0f, 0.4f)] private float _bottomMargin = 0.3f;

        private Camera _camera;
        private float _fittedAspect;

        public float TopMargin => _topMargin;
        public float BottomMargin => _bottomMargin;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void LateUpdate()
        {
            // Re-fit only when the screen shape changes (e.g. the Game view is resized or the phone rotates).
            if (!Mathf.Approximately(_camera.aspect, _fittedAspect))
            {
                Fit();
            }
        }

        public void Fit()
        {
            _fittedAspect = _camera.aspect;

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

            // Find the closest distance where every corner fits on screen between the margins.
            // Moving the camera back only ever makes things fit better, so a binary search works.
            float near = 0.1f;
            float far = 1000f;
            for (int i = 0; i < 40; i++)
            {
                float middle = (near + far) / 2f;
                if (TryFit(corners, middle, out _)) far = middle;
                else near = middle;
            }

            TryFit(corners, far, out float shift);
            // Keeping the angle fixed, move back by the distance and slide down/up by the shift.
            transform.position = bounds.center - transform.forward * far - transform.up * shift;
        }

        /// <summary>
        /// Checks if all corners fit when the camera is <paramref name="distance"/> away from the board center.
        /// <paramref name="shift"/> is how far to slide the camera down to center the board in the free band.
        /// </summary>
        private bool TryFit(Vector3[] corners, float distance, out float shift)
        {
            float tanVertical = Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float tanHorizontal = tanVertical * _camera.aspect;
            float bandBottom = tanVertical * (2f * _bottomMargin - 1f); // screen positions as slopes
            float bandTop = tanVertical * (1f - 2f * _topMargin);

            float lowestShift = float.MinValue;
            float highestShift = float.MaxValue;
            foreach (Vector3 corner in corners)
            {
                float depth = corner.z + distance;
                if (depth <= 0.01f || Mathf.Abs(corner.x) > tanHorizontal * depth)
                {
                    shift = 0f;
                    return false;
                }
                lowestShift = Mathf.Max(lowestShift, bandBottom * depth - corner.y);
                highestShift = Mathf.Min(highestShift, bandTop * depth - corner.y);
            }

            shift = (lowestShift + highestShift) / 2f;
            return lowestShift <= highestShift;
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
