using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

namespace TileMatch.Core
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Win Panel")]
        [SerializeField] private GameObject winPanel;
        [SerializeField] private TMP_Text winTitleText;
        [SerializeField] private Button winRetryButton;

        [Header("Fail Panel")]
        [SerializeField] private GameObject failPanel;
        [SerializeField] private TMP_Text failTitleText;
        [SerializeField] private Button failRetryButton;

        [Header("Animation Settings")]
        [SerializeField] private float panelAnimDuration = 0.5f;

        private void Awake()
        {
            if (Instance != null && Instance != this) Destroy(gameObject);
            else Instance = this;

            // Başlangıçta panelleri gizle
            if (winPanel != null) winPanel.SetActive(false);
            if (failPanel != null) failPanel.SetActive(false);

            // Buton eventlerini bağla
            if (winRetryButton != null) winRetryButton.onClick.AddListener(OnRetryClicked);
            if (failRetryButton != null) failRetryButton.onClick.AddListener(OnRetryClicked);
        }

        public void ShowWinPanel()
        {
            if (winPanel == null) return;

            winPanel.SetActive(true);

            // Paneli animasyonla göster
            CanvasGroup cg = winPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = winPanel.AddComponent<CanvasGroup>();

            cg.alpha = 0f;
            cg.interactable = false;

            RectTransform rt = winPanel.GetComponent<RectTransform>();
            rt.localScale = Vector3.one * 0.5f;

            // Sequence: Fade in + Scale up
            Sequence seq = DOTween.Sequence();
            seq.Append(cg.DOFade(1f, panelAnimDuration).SetEase(Ease.OutQuad));
            seq.Join(rt.DOScale(Vector3.one, panelAnimDuration).SetEase(Ease.OutBack));
            seq.OnComplete(() => {
                cg.interactable = true;
            });
        }

        public void ShowFailPanel()
        {
            if (failPanel == null) return;

            failPanel.SetActive(true);

            // Paneli animasyonla göster
            CanvasGroup cg = failPanel.GetComponent<CanvasGroup>();
            if (cg == null) cg = failPanel.AddComponent<CanvasGroup>();

            cg.alpha = 0f;
            cg.interactable = false;

            RectTransform rt = failPanel.GetComponent<RectTransform>();
            rt.localScale = Vector3.one * 0.5f;

            // Sequence: Fade in + Scale up (daha sert bir his için)
            Sequence seq = DOTween.Sequence();
            seq.Append(cg.DOFade(1f, panelAnimDuration).SetEase(Ease.OutQuad));
            seq.Join(rt.DOScale(Vector3.one, panelAnimDuration).SetEase(Ease.OutBounce));
            seq.OnComplete(() => {
                cg.interactable = true;
            });
        }

        private void OnRetryClicked()
        {
            // Sahneyi yeniden yükle
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
            );
        }
    }
}
