using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Frictionless;
using Solana.Unity.Programs.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using SolanaTwentyfourtyeight.Accounts;
using SolanaTwentyfourtyeight.Types;
using SolPlay.DeeplinksNftExample.Utils;
using SolPlay.Scripts.Services;
using TMPro;
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
        public TextMeshProUGUI CurrentHighscoreText;

        public Button BackgroundButton;
        public Button AddOneSolButton;
        public GameObject ShowAnotherPlayerFieldRoot;
        public TextMeshProUGUI OtherPlayerWalletAddressText;
        public TextMeshProUGUI OtherPlayerNftNameText;

        private Highscore currentHighscore;
        private bool weekly = true;
        private double currentPricePool;
        
        private new void Awake()
        {
            base.Awake();
            WeeklyButton.onClick.AddListener(OnWeeklyButtonClicked);
            GlobalButton.onClick.AddListener(OnGlobalButtonClicked);
            AddOneSolButton.onClick.AddListener(OnAddOneSolButtonClicked);
            GlobalButtonCanvasGroup.alpha = 0.5f;
            WeeklyButtonCanvasGroup.alpha = 1f;
            GlobalArrow.gameObject.SetActive(false);
            WeeklyArrow.gameObject.SetActive(true);
            BackgroundButton.onClick.AddListener(OnBackgroundButtonClicked);
            ShowAnotherPlayerFieldRoot.SetActive(false);
        }

        private void OnBackgroundButtonClicked()
        {
            Root.gameObject.SetActive(true);
            ShowAnotherPlayerFieldRoot.SetActive(false);
            Solana2048Service.Instance.SubscribeToPlayerDataUpdates();
            ServiceFactory.Resolve<BoardManager>().IsWaiting = false;
        }

        private void OnAddOneSolButtonClicked()
        {
            Web3.Wallet.Transfer(Solana2048Service.Instance.PricePoolPDA, SolanaUtils.SolToLamports / 10, Commitment.Confirmed);
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
            Solana2048Service.OnPricePoolChanged += OnPricePoolChanged;
            base.Open(uiData);
        }

        private void OnPricePoolChanged(string newPricePool)
        {
            CurrentHighscoreText.text = newPricePool;
        }

        public override void Close()
        {
            Solana2048Service.OnHighscoreChanged -= OnHighscoreChanged;
            Solana2048Service.OnPricePoolChanged -= OnPricePoolChanged;
            base.Close();
        }

        private async void OnHighscoreChanged(Highscore highscore)
        {
            currentHighscore = highscore;

            currentPricePool = await Web3.Wallet.GetBalance(Solana2048Service.Instance.PricePoolPDA);
            CurrentHighscoreText.text = currentPricePool.ToString("F3");
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
                highscoreEntryInstance.SetData(highscoreEntry, count, currentPricePool, weekly, onClick: async  entry =>
                {
                    await ShowPreview(entry);
                });
                count++;
            }
        }

        private async Task ShowPreview(HighscoreListEntry entry)
        {
            OtherPlayerNftNameText.text = entry.Nft.metaplexData.data.offchainData.name;
            OtherPlayerWalletAddressText.text = entry.CurrentHighscoreEntry.Player;
            ServiceFactory.Resolve<BoardManager>().IsWaiting = true;
            PublicKey.TryFindProgramAddress(new[]
                {
                    Encoding.UTF8.GetBytes("player7"), entry.CurrentHighscoreEntry.Player,
                    entry.CurrentHighscoreEntry.Nft
                },
                Solana2048Service.Solana_2048_ProgramIdPubKey, out PublicKey PlayerDataPDA, out byte bump);

            AccountResultWrapper<PlayerData> playerData = null;

            try
            {
                playerData =
                    await Solana2048Service.Instance.solana_2048_client.GetPlayerDataAsync(PlayerDataPDA, Commitment.Confirmed);
            }
            catch (Exception e)
            {
                Debug.LogWarning("Could not get player data: " + e);
            }

            if (playerData != null && playerData.ParsedResult != null)
            {
                Root.gameObject.SetActive(false);
                ShowAnotherPlayerFieldRoot.SetActive(true);
                ServiceFactory.Resolve<BoardManager>().RefreshFromPlayerdata(playerData.ParsedResult);
            }
        }
    }
}