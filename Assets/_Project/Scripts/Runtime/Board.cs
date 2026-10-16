using UnityEngine;

namespace RollOfLegends
{
    /// <summary>
    /// The loop of tiles the hero walks on.
    /// Every direct child of this object is one tile, and the order in the Hierarchy is the walking order.
    /// Child 0 is the START tile.
    /// </summary>
    public class Board : MonoBehaviour
    {
        public int TileCount => transform.childCount;

        public Vector3 GetTilePosition(int index)
        {
            return transform.GetChild(index).position;
        }

        /// <summary>Moves <paramref name="steps"/> tiles forward from <paramref name="current"/>, wrapping around the loop.</summary>
        public static int Advance(int current, int steps, int tileCount)
        {
            return (current + steps) % tileCount;
        }
    }
}
