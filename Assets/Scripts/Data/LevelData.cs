using System.Collections.Generic;
using UnityEngine;

namespace TileMatch.Data
{
    [CreateAssetMenu(fileName = "NewLevelData", menuName = "TileMatch/Level Data", order = 1)]
    public class LevelData : ScriptableObject
    {
        [System.Serializable]
        public class TilePlacement
        {
            public int x;
            public int y;
            public int z;
            [Tooltip("The ID representing the kind of tile (e.g. 0=Apple, 1=Banana)")]
            public int tileTypeId;
        }

        [System.Serializable]
        public class OrderSequence
        {
            [Tooltip("An array of 3 tileTypeIds for this order")]
            public int[] requiredTileTypeIds = new int[3];
        }

        public List<TilePlacement> initialTiles = new List<TilePlacement>();
        public List<OrderSequence> levelOrders = new List<OrderSequence>();
    }
}
