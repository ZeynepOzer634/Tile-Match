using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TileMatch.Data;
using TileMatch.Board;
using TileMatch.Core;

namespace TileMatch.Orders
{
    public class OrderManager : MonoBehaviour
    {
        public static OrderManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private Transform orderTrayTransform; // Where to visually send the tile
        [Header("UI Settings")]
        [SerializeField] private Image[] orderRequirementUIImages;
        
        private Queue<LevelData.OrderSequence> _orderQueue = new Queue<LevelData.OrderSequence>();
        private LevelData.OrderSequence _currentOrder;
        private int _currentOrderProgressIndex = 0; // 0 to 2

        private void Awake()
        {
            if (Instance != null && Instance != this) Destroy(gameObject);
            else Instance = this;
        }

        private void UpdateOrderUI()
        {
            if (_currentOrder == null)
            {
                foreach(var img in orderRequirementUIImages) img.gameObject.SetActive(false);
                return;
            }

            for (int i = 0; i < 3; i++)
            {
                if (i < _currentOrderProgressIndex)
                {
                    orderRequirementUIImages[i].gameObject.SetActive(false);
                }
                else
                {
                    int neededTileId = _currentOrder.requiredTileTypeIds[i];
                    orderRequirementUIImages[i].sprite = BoardManager.Instance.GetSpriteForTile(neededTileId);
                    orderRequirementUIImages[i].gameObject.SetActive(true);
                    
                    // Board taşları gibi pop animasyonu
                    orderRequirementUIImages[i].transform.localScale = Vector3.zero;
                    orderRequirementUIImages[i].transform.DOScale(Vector3.one, 0.3f)
                        .SetDelay(i * 0.1f) // Her ikon sırayla pop olsun
                        .SetEase(Ease.OutBack);
                }
            }
        }

        public void Initialize(List<LevelData.OrderSequence> orders)
        {
            _orderQueue.Clear();
            foreach (var order in orders)
            {
                _orderQueue.Enqueue(order);
            }
            SetupNextOrder();
        }

        private void SetupNextOrder()
        {
            if (_orderQueue.Count > 0)
            {
                _currentOrder = _orderQueue.Dequeue();
                _currentOrderProgressIndex = 0;
                Debug.Log($"New Order Assigned! Needs: {_currentOrder.requiredTileTypeIds[0]}, {_currentOrder.requiredTileTypeIds[1]}, {_currentOrder.requiredTileTypeIds[2]}");
                
                UpdateOrderUI();

                // Immediately check if rack holds what we need next!
                GameManager.Instance.CheckCustomRackMatches();
            }
            else
            {
                _currentOrder = null;
                UpdateOrderUI();
                // No more orders, we won!
                GameManager.Instance.LevelWon();
            }
        }

        public int GetNextRequiredTileId()
        {
            // Eğer o anki sipariş bittiyse (Progress 3 olduysa) ve taşın gelmesi bekleniyorsa yeni taş alma
            if (_currentOrder == null || _currentOrderProgressIndex >= 3) return -1;
            return _currentOrder.requiredTileTypeIds[_currentOrderProgressIndex];
        }

        public bool TryProcessTile(Tile tile)
        {
            if (_currentOrder == null) return false;

            if (tile.TileTypeId == GetNextRequiredTileId())
            {
                ConsumeTile(tile);
                return true; // Match found, absorbed
            }

            return false; // Does not match
        }

        public void ConsumeTile(Tile tile)
        {
            // O anki taşın UI çerçeve sırasını kaydet (0, 1 veya 2)
            int visualIndex = _currentOrderProgressIndex;
            
            // Mantıksal olarak siparişi ilerlet ki, hızlı arka arkaya basmalarda sistem sıradaki taşı bilsin
            _currentOrderProgressIndex++;

            // Hedef olarak, ortaya değil doğrudan o sıradaki UI ikonunun pozisyonuna git
            Vector3 targetPos = orderTrayTransform.position;
            if (visualIndex < 3 && orderRequirementUIImages != null && orderRequirementUIImages[visualIndex] != null)
            {
                targetPos = orderRequirementUIImages[visualIndex].transform.position;
            }

            // Taşı fiziksel olarak ilgili sipariş kutusuna gönder (hideBackground = true yaparak arkaplanı kapat)
            tile.MoveToTarget(targetPos, () => {
                Destroy(tile.gameObject);
                
                // --- TAŞ EFEKTİ ULAŞTIĞINDA ÇALIŞACAK KISIM ---
                // Taş hedefine vardığında ekrandaki ilgili UI resmini gizle
                if (visualIndex < 3 && orderRequirementUIImages[visualIndex] != null)
                {
                    orderRequirementUIImages[visualIndex].gameObject.SetActive(false);
                }

                if (visualIndex >= 2)
                {
                    // Siparişteki 3 taş da geldi ve yok oldu. Şimdi yeni siparişi başlat.
                    SetupNextOrder();
                }
            }, true, true); // hideBackground = true, shrinkOnArrival = true

            // Eğer bu siparişin son taşı değilse rafı kontrol et
            if (_currentOrderProgressIndex < 3)
            {
                // Check if the NEXT item is already sitting in the rack
                GameManager.Instance.CheckCustomRackMatches();
            }
        }
    }
}
