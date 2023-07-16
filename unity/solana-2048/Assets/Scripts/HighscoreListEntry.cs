using System;
using Cysharp.Threading.Tasks;
using Lumberjack.Types;
using Solana.Unity.SDK;
using Solana.Unity.SDK.Nft;
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

        public async void SetData(HighscoreEntry highscoreEntry)
        {
            HighscoreText.text = highscoreEntry.Score.ToString();
            WalletAddress.text = highscoreEntry.Nft;

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
            
            NftItemView.gameObject.SetActive(nft != null);
            FallbackImage.gameObject.SetActive(nft == null);

            if (nft != null)
            {
                NftItemView.SetData(nft, view => {});
            }
        }
    }
}