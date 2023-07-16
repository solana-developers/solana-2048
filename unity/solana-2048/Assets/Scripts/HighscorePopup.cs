using System.Linq;
using Lumberjack.Accounts;
using Lumberjack.Types;
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

        private Highscore currentHighscore;
        private bool weekly = true;
        
        private void Awake()
        {
            WeeklyButton.onClick.AddListener(OnWeeklyButtonClicked);
            GlobalButton.onClick.AddListener(OnGlobalButtonClicked);
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

        private void OnHighscoreChanged(Highscore highscore)
        {
            currentHighscore = highscore;
            
            LoadingSpinner.gameObject.SetActive(false);

            UpdateContent();
        }

        private void UpdateContent()
        {
            GlobalButtonCanvasGroup.alpha = weekly ? 0.5f:  1;
            WeeklyButtonCanvasGroup.alpha = !weekly ? 0.5f:  1f;
            GlobalArrow.gameObject.SetActive(weekly);
            WeeklyArrow.gameObject.SetActive(!weekly);
            
            foreach (Transform trans in HighscoreListEntryRoot.transform)
            {
                Destroy(trans.gameObject);
            }
            
            IOrderedEnumerable<HighscoreEntry> sortedScores;
                sortedScores = weekly ?  currentHighscore.Weekly.OrderByDescending(score=>score.Score) :  currentHighscore.Global.OrderByDescending(score=>score.Score);

            foreach (var highscoreEntry in sortedScores)
            {
                var highscoreEntryInstance = Instantiate(HighscoreListEntry, HighscoreListEntryRoot.transform);
                highscoreEntryInstance.SetData(highscoreEntry);
            }
        }
    }
}