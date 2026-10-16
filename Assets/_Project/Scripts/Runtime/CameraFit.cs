using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// Keeps the camera's angle fixed, but moves it closer or further away so the whole board
    /// fits on any screen shape (portrait phone, landscape phone, Editor Game view).
    /// To change the viewing angle, rotate the camera in the Scene view.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraFit : MonoBehaviour
    {
        [SerializeField] private Board _board;

        [Tooltip("Extra empty space around the board, in world units.")]
        [SerializeField] private float _padding = 0.3f;

        private Camera _camera;
        private float _fittedAspect;

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
            float radius = bounds.extents.magnitude + _padding;

            // The narrower of the vertical and horizontal field of view decides how far back the camera must be.
            float halfVertical = _camera.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfHorizontal = Mathf.Atan(Mathf.Tan(halfVertical) * _camera.aspect);
            float halfFov = Mathf.Min(halfVertical, halfHorizontal);

            float distance = radius / Mathf.Sin(halfFov);
            transform.position = bounds.center - transform.forward * distance;
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
