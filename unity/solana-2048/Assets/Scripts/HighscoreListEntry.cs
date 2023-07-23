using System;
using Cysharp.Threading.Tasks;
using Frictionless;
using SolanaTwentyfourtyeight.Types;
using Solana.Unity.SDK;
using Solana.Unity.SDK.Nft;
using SolPlay.Scripts.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SolPlay.Scripts.Ui
{
    public class HighscoreListEntry : MonoBehaviour
    {
        public NftItemView NftItemView;
        public TextMeshProUGUI HighscoreText;
        public TextMeshProUGUI WalletAddress;
        public TextMeshProUGUI NftName;
        public Image FallbackImage;

        public Sprite[] TrophieSprites;
        public Image TrophieImage;
        public Image SelectedBorder;
        public TextMeshProUGUI SolReward;
        public GameObject SolRewardRoot;
        public Button Button;
        public Nft Nft;
        public HighscoreEntry CurrentHighscoreEntry { get; private set; }

        private Action<HighscoreListEntry> OnClick;
        
        private void Awake()
        {
            Button.onClick.AddListener(OnButtonClicked);
        }

        private void OnButtonClicked()
        {
            OnClick?.Invoke(this);
        }

        public async void SetData(HighscoreEntry highscoreEntry, int count, double jackpotLamports, bool weekly, Action<HighscoreListEntry> onClick)
        {
            CurrentHighscoreEntry = highscoreEntry;
            OnClick = onClick;
            HighscoreText.text = highscoreEntry.Score.ToString();
            WalletAddress.text = highscoreEntry.Player;

            if (weekly)
            {
                if (count < 3)
                {
                    TrophieImage.sprite = TrophieSprites[count];
                }
                TrophieImage.gameObject.SetActive(count < 3);
                SolRewardRoot.gameObject.SetActive(count < 3);
                SolReward.text = (jackpotLamports / 3).ToString("F3");
            }
            else
            {
                TrophieImage.gameObject.SetActive(false);
                SolRewardRoot.gameObject.SetActive(false);
            }
            
            SelectedBorder.gameObject.SetActive(highscoreEntry.Player == Web3.Account.PublicKey);

            
            Nft = null;
            try
            {
                var rpc = Web3.Wallet.ActiveRpcClient;
                Nft  = await Nft.TryGetNftData(highscoreEntry.Nft, rpc).AsUniTask();
            }
            catch (Exception e)
            {
                Debug.LogError("Could not load nft" + e);
            }

            if (Nft == null)
            {
                Nft = ServiceFactory.Resolve<NftService>().CreateDummyLocalNft(highscoreEntry.Nft, highscoreEntry.Player);
            }
            NftItemView.gameObject.SetActive(Nft != null);
            FallbackImage.gameObject.SetActive(Nft == null);

            NftName.text = Nft.metaplexData.data.metadata.name;

            if (Nft != null)
            {
                NftItemView.SetData(Nft, view => {});
            }
        }

    }
}