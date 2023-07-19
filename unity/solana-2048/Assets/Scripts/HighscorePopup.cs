using System.Linq;
using Solana.Unity.SDK;
using SolanaTwentyfourtyeight.Accounts;
using SolanaTwentyfourtyeight.Types;
using SolPlay.Scripts.Services;
using UnityEngine;
using UnityEngine.UI;

namespace SolPlay.Scripts.Ui
{
    /// <summary>
    /// Show the current highscore list when opened
    /// </summary>
    public class HighscorePopup : BasePopup
    {
        public GameObject LoadingSpinner;
        public GameObject HighscoreListEntryRoot;
        public HighscoreListEntry HighscoreListEntry;
        public Button WeeklyButton;
        public Button GlobalButton;
        public CanvasGroup WeeklyButtonCanvasGroup;
        public CanvasGroup GlobalButtonCanvasGroup;
        public GameObject GlobalArrow;
        public GameObject WeeklyArrow;
        public Button ResetWeeklyHighscore;

        private Highscore currentHighscore;
        private bool weekly = true;
        private double currentPricePool;
        
        private void Awake()
        {
            WeeklyButton.onClick.AddListener(OnWeeklyButtonClicked);
            GlobalButton.onClick.AddListener(OnGlobalButtonClicked);
            ResetWeeklyHighscore.onClick.AddListener(OnResetWeeklyHighscoreClicked);
            GlobalButtonCanvasGroup.alpha = 0.5f;
            WeeklyButtonCanvasGroup.alpha = 1f;
            GlobalArrow.gameObject.SetActive(false);
            WeeklyArrow.gameObject.SetActive(true);
        }

        private void OnGlobalButtonClicked()
        {
            weekly = false;
            UpdateContent();
        }

        private void OnWeeklyButtonClicked()
        {
            weekly = true;
            UpdateContent();
        }

        private void OnResetWeeklyHighscoreClicked()
        {
            Solana2048Service.Instance.ResetWeeklyHighscore();
        }
        
        public override void Open(UiService.UiData uiData)
        {
            Solana2048Service.Instance.RequestHighscore();
            LoadingSpinner.gameObject.SetActive(true);
            Solana2048Service.OnHighscoreChanged += OnHighscoreChanged;
            base.Open(uiData);
        }

        public override void Close()
        {
            Solana2048Service.OnHighscoreChanged -= OnHighscoreChanged;
            base.Close();
        }

        private async void OnHighscoreChanged(Highscore highscore)
        {
            currentHighscore = highscore;

            currentPricePool = await Web3.Wallet.GetBalance(Solana2048Service.Instance.PricePoolPDA);
            
            LoadingSpinner.gameObject.SetActive(false);

            UpdateContent();
        }

        private void UpdateContent()
        {
            GlobalButtonCanvasGroup.alpha = weekly ? 0.5f:  1;
            WeeklyButtonCanvasGroup.alpha = !weekly ? 0.5f:  1f;
            GlobalArrow.gameObject.SetActive(!weekly);
            WeeklyArrow.gameObject.SetActive(weekly);
            
            foreach (Transform trans in HighscoreListEntryRoot.transform)
            {
                Destroy(trans.gameObject);
            }
            
            IOrderedEnumerable<HighscoreEntry> sortedScores;
                sortedScores = weekly ?  currentHighscore.Weekly.OrderByDescending(score=>score.Score) :  currentHighscore.Global.OrderByDescending(score=>score.Score);

            int count = 0;
            foreach (var highscoreEntry in sortedScores)
            {
                var highscoreEntryInstance = Instantiate(HighscoreListEntry, HighscoreListEntryRoot.transform);
                highscoreEntryInstance.SetData(highscoreEntry, count, currentPricePool);
                count++;
            }
        }
    }
}