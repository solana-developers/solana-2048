using System.Linq;
using Solana2048.Accounts;
using SolPlay.Scripts.Services;
using UnityEngine;

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
            LoadingSpinner.gameObject.SetActive(false);
            foreach (Transform trans in HighscoreListEntryRoot.transform)
            {
                Destroy(trans.gameObject);
            }
            
            var sortedScores = highscore.Data.OrderByDescending(score=>score.Score);

            foreach (var highscoreEntry in sortedScores)
            {
                var highscoreEntryInstance = Instantiate(HighscoreListEntry, HighscoreListEntryRoot.transform);
                highscoreEntryInstance.SetData(highscoreEntry);
                
            }
        }
    }
}