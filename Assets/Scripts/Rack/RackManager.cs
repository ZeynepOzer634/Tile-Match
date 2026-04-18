using System.Collections.Generic;
using UnityEngine;
using TileMatch.Board;
using TileMatch.Core;

namespace TileMatch.Rack
{
    public class RackManager : MonoBehaviour
    {
        public static RackManager Instance { get; private set; }

        private const int MAX_SLOTS = 6;
        private List<Tile> _rackTiles = new List<Tile>();

        [Header("Settings")]
        [SerializeField] private Transform[] slotTransforms; // Visually max 6 transforms
        
        private void Awake()
        {
            if (Instance != null && Instance != this) Destroy(gameObject);
            else Instance = this;
        }

        public void Initialize()
        {
            foreach(var tile in _rackTiles)
            {
                if(tile != null) Destroy(tile.gameObject);
            }
            _rackTiles.Clear();
        }

        public void AddToRack(Tile tile)
        {
            _rackTiles.Add(tile);

            // Reorganize visual
            UpdateRackVisuals();

            // Check Fail Condition
            if (_rackTiles.Count >= MAX_SLOTS)
            {
                GameManager.Instance.LevelFailed();
            }
        }

        private void UpdateRackVisuals()
        {
            for (int i = 0; i < _rackTiles.Count; i++)
            {
                if (i < slotTransforms.Length)
                {
                    _rackTiles[i].MoveToTarget(slotTransforms[i].position, null, true);
                }
            }
        }

        // Extracts a specific tile if it exists in the rack
        public Tile ExtractTileOfType(int typeId)
        {
            for (int i = 0; i < _rackTiles.Count; i++)
            {
                if (_rackTiles[i].TileTypeId == typeId)
                {
                    Tile found = _rackTiles[i];
                    _rackTiles.RemoveAt(i);
                    UpdateRackVisuals(); // Fill the gap
                    return found;
                }
            }
            return null;
        }
    }
}
