using UnityEngine;

namespace RollOfLegends
{
    /// <summary>Keeps a world-space label turned toward the camera so it is always readable.</summary>
    public class FaceCamera : MonoBehaviour
    {
        private void LateUpdate()
        {
            Camera camera = Camera.main;
            if (camera != null)
            {
                transform.rotation = camera.transform.rotation;
            }
        }
    }
}
