using System.Collections.Generic;
using UnityEngine;
using TileMatch.Data;
using TileMatch.Core;

namespace TileMatch.Board
{
    public class BoardManager : MonoBehaviour
    {
        public static BoardManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private Transform boardParent;
        [SerializeField] private Tile tilePrefab;
        [SerializeField] private Vector3 tileWorldSpacing = new Vector3(0.5f, 0.5f, 0.2f);
        [SerializeField] private Sprite[] tileSprites; // Map tileTypeId to Sprite for simple prototyping

        // Dictionary to track which tile occupies which grid coordinates
        // By scaling coordinates (1 tile = 2x2 cells), we can solve half-offsets easily.
        private Dictionary<Vector3Int, Tile> _activeTiles = new Dictionary<Vector3Int, Tile>();

        private void Awake()
        {
            if (Instance != null && Instance != this) Destroy(gameObject);
            else Instance = this;
        }

        public Sprite GetSpriteForTile(int tileTypeId)
        {
            if (tileTypeId >= 0 && tileTypeId < tileSprites.Length)
                return tileSprites[tileTypeId];
            return null;
        }

        public void LoadLevel(LevelData levelData)
        {
            _activeTiles.Clear();

            // Z'ye göre sırala (düşük Z önce), eğer Z'ler aynıysa Y'ye göre sırala (Büyük Y önce)
            // Daha sonra X'e göre sırala (Küçük X önce).
            // Bu, objeleri Sol-Üstten (Top-Left) Sağ-Alta (Bottom-Right) doğru yaratır, 
            // böylece sağ alttaki objeler sol üsttekilerin üzerine biner (2.5D derinliği için kusursuzdur).
            var sortedPlacements = new List<LevelData.TilePlacement>(levelData.initialTiles);
            sortedPlacements.Sort((a, b) => 
            {
                int zCmp = a.z.CompareTo(b.z);
                if (zCmp != 0) return zCmp;
                
                int yCmp = b.y.CompareTo(a.y); // Y: Büyükten Küçüğe (Yukarıdan Aşağıya)
                if (yCmp != 0) return yCmp;
                
                return a.x.CompareTo(b.x); // X: Küçükten Büyüğe (Soldan Sağa)
            });

            foreach (var placement in sortedPlacements)
            {
                // Instantiate visuals
                Vector3 localPos = new Vector3(
                    placement.x * tileWorldSpacing.x, 
                    placement.y * tileWorldSpacing.y, 
                    0 // UI'da Z pozisyonu kullanmıyoruz, sıralama hiyerarşi ile yapılıyor
                );

                Tile newTile = Instantiate(tilePrefab, boardParent);
                newTile.transform.localPosition = localPos;
                newTile.transform.localRotation = Quaternion.identity;
                newTile.transform.localScale = Vector3.one;
                
                // Calculate occupied keys (1 tile occupies a 2x2 coordinate block locally to handle half-overlaps if desired)
                Vector3Int baseCoord = new Vector3Int(placement.x, placement.y, placement.z);
                List<Vector3Int> occupiedKeys = new List<Vector3Int>()
                {
                    new Vector3Int(placement.x, placement.y, placement.z),
                    new Vector3Int(placement.x + 1, placement.y, placement.z),
                    new Vector3Int(placement.x, placement.y + 1, placement.z),
                    new Vector3Int(placement.x + 1, placement.y + 1, placement.z)
                };

                Sprite icon = null;
                if (placement.tileTypeId >= 0 && placement.tileTypeId < tileSprites.Length)
                {
                    icon = tileSprites[placement.tileTypeId];
                }

                newTile.Initialize(placement.tileTypeId, baseCoord, occupiedKeys, icon);

                // Register in Dictionary
                foreach (var key in occupiedKeys)
                {
                    _activeTiles[key] = newTile;
                }
            }
        }

        public void OnTileClicked(Tile tile)
        {
            if (!GameManager.Instance.IsPlaying) return;

            if (IsTileBlocked(tile))
            {
                Debug.Log($"Tile at {tile.BaseCoordinate} is blocked by higher layers!");
                return; // Do nothing, tile is obscured
            }

            // Unblocked! Process it.
            RemoveTileFromBoard(tile);
            
            GameManager.Instance.ProcessClickedTile(tile);
        }

        private bool IsTileBlocked(Tile tile)
        {
            // The mathematical check using the Dictionary
            foreach (var key in tile.OccupiedKeys)
            {
                // Sadece 1 üst kata değil, üzerindeki TÜM katmanlara (Z eksenine) bakalım
                for (int z = key.z + 1; z <= 20; z++)
                {
                    Vector3Int aboveKey = new Vector3Int(key.x, key.y, z);
                    if (_activeTiles.ContainsKey(aboveKey))
                    {
                        // Üzerinde herhangi bir Z katmanında taşı kapatan bir obje var
                        return true;
                    }
                }
            }
            return false;
        }

        private void RemoveTileFromBoard(Tile tile)
        {
            foreach (var key in tile.OccupiedKeys)
            {
                _activeTiles.Remove(key);
            }
        }
    }
}
