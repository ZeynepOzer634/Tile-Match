using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;
using TileMatch.Core;
using TileMatch.Board;

namespace TileMatch.Board
{
    public class Tile : MonoBehaviour, IPointerClickHandler
    {
        public int TileTypeId { get; private set; }
        
        [Header("References")]
        [SerializeField] private Image tileImage;

        // The base coordinate, used if we want to remove the tile later
        public Vector3Int BaseCoordinate { get; private set; }
        
        // All dictionary keys this tile occupies (handles half-tile overlaps natively)
        public List<Vector3Int> OccupiedKeys { get; private set; } = new List<Vector3Int>();

        public void Initialize(int typeId, Vector3Int baseCoord, List<Vector3Int> occupiedKeys, Sprite iconSprite)
        {
            TileTypeId = typeId;
            BaseCoordinate = baseCoord;
            OccupiedKeys = occupiedKeys;
            
            if (tileImage != null && iconSprite != null)
            {
                tileImage.sprite = iconSprite;
            }

            // Simple intro animation
            transform.localScale = Vector3.zero;
            transform.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // Inform the BoardManager that this tile was clicked
            BoardManager.Instance.OnTileClicked(this);
        }

        public void MoveToTarget(Vector3 targetPosition, System.Action onComplete = null, bool hideBackground = false, bool shrinkOnArrival = false)
        {
            // Tüm mevcut animasyonları durdur
            transform.DOKill();

            Sequence seq = DOTween.Sequence();

            // 1) Toplama efekti: Hafifçe büyü (punch)
            seq.Append(transform.DOScale(Vector3.one * 1.2f, 0.1f).SetEase(Ease.OutQuad));
            seq.AppendCallback(() => {
                if (hideBackground)
                {
                    Image bgImage = GetComponent<Image>();
                    if (bgImage != null) bgImage.enabled = false;
                }
            });
            seq.Append(transform.DOScale(Vector3.one, 0.1f).SetEase(Ease.InQuad));

            // 2) Hedefe doğru süzül
            seq.Append(transform.DOMove(targetPosition, 0.35f).SetEase(Ease.InOutQuad));

            // 3) Sadece siparişe gidiyorsa küçülerek yok ol
            if (shrinkOnArrival)
            {
                seq.Append(transform.DOScale(Vector3.zero, 0.15f).SetEase(Ease.InBack));
            }

            // 4) Animasyon bitince callback çağır
            seq.OnComplete(() => {
                onComplete?.Invoke();
            });
        }
    }
}
