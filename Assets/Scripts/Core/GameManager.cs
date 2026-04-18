using UnityEngine;
using TileMatch.Board;
using TileMatch.Data;
using TileMatch.Orders;
using TileMatch.Rack;

namespace TileMatch.Core
{
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public bool IsPlaying { get; private set; }

        [Header("Level Testing")]
        [SerializeField] private LevelData currentLevel;

        private const float INPUT_COOLDOWN = 0.4f; // Tıklamalar arası minimum bekleme süresi
        private float _lastInputTime = -1f;

        private void Awake()
        {
            if (Instance != null && Instance != this) Destroy(gameObject);
            else Instance = this;
        }

        private void Start()
        {
            if (currentLevel != null)
            {
                StartLevel(currentLevel);
            }
            else
            {
                Debug.LogWarning("No LevelData assigned to GameManager.");
            }
        }

        public void StartLevel(LevelData level)
        {
            IsPlaying = true;
            BoardManager.Instance.LoadLevel(level);
            RackManager.Instance.Initialize();
            OrderManager.Instance.Initialize(level.levelOrders);
        }

        public void ProcessClickedTile(Tile tile)
        {
            if (!IsPlaying) return;

            // Spam koruması: Son tıklamadan bu yana yeterince zaman geçmemiş ise işleme
            if (Time.time - _lastInputTime < INPUT_COOLDOWN) return;
            _lastInputTime = Time.time;

            // 1) Ask OrderManager if it needs this
            bool takenByOrder = OrderManager.Instance.TryProcessTile(tile);

            // 2) If not, push to rack
            if (!takenByOrder)
            {
                RackManager.Instance.AddToRack(tile);
            }
        }

        public void CheckCustomRackMatches()
        {
            if (!IsPlaying) return;

            int requiredId = OrderManager.Instance.GetNextRequiredTileId();
            if (requiredId == -1) return;

            Tile rackTile = RackManager.Instance.ExtractTileOfType(requiredId);
            if (rackTile != null)
            {
                Debug.Log("Rack supplied the required tile!");
                OrderManager.Instance.ConsumeTile(rackTile);
            }
        }

        public void LevelWon()
        {
            IsPlaying = false;
            Debug.Log("LEVEL WON! All orders completed.");
            UIManager.Instance.ShowWinPanel();
        }

        public void LevelFailed()
        {
            IsPlaying = false;
            Debug.Log("LEVEL FAILED! Rack is full.");
            UIManager.Instance.ShowFailPanel();
        }
    }
}
