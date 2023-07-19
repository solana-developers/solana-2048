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

        public async void SetData(HighscoreEntry highscoreEntry, int count, double jackpotLamports)
        {
            HighscoreText.text = highscoreEntry.Score.ToString();
            WalletAddress.text = highscoreEntry.Nft;

            if (SelectedBorder != null)
            {
                SelectedBorder.gameObject.SetActive(highscoreEntry.Player == Web3.Account.PublicKey);
                TrophieImage.gameObject.SetActive(count < 3);
                TrophieImage.sprite = TrophieSprites[count];
            }

            SolReward.gameObject.SetActive(count < 3);
            SolReward.text = (jackpotLamports / 3).ToString("F3") + " sol";
            
            Nft nft = null;
            try
            {
                var rpc = Web3.Wallet.ActiveRpcClient;
                nft  = await Nft.TryGetNftData(highscoreEntry.Nft, rpc).AsUniTask();
            }
            catch (Exception e)
            {
                Debug.LogError("Could not load nft" + e);
            }

            if (nft == null)
            {
                nft = ServiceFactory.Resolve<NftService>().CreateDummyLocalNft(highscoreEntry.Nft);
            }
            NftItemView.gameObject.SetActive(nft != null);
            FallbackImage.gameObject.SetActive(nft == null);

            NftName.text = nft.metaplexData.data.metadata.name;

            if (nft != null)
            {
                NftItemView.SetData(nft, view => {});
            }
        }
    }
}