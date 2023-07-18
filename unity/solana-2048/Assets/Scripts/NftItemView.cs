using System;
using System.Text;
using System.Threading.Tasks;
using Frictionless;
using SolanaTwentyfourtyeight.Accounts;
using Solana.Unity.Programs.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.SDK.Nft;
using SolPlay.Scripts.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;

namespace SolPlay.Scripts.Ui
{
    /// <summary>
    /// Show the image of a given Nft and can have a click handler
    /// </summary>
    public class NftItemView : MonoBehaviour
    {
        public Nft CurrentSolPlayNft;
        public RawImage Icon;
        public TextMeshProUGUI Headline;
        public TextMeshProUGUI Description;
        public TextMeshProUGUI ErrorText;
        public TextMeshProUGUI ScoreText;
        public Button Button;
        public GameObject SelectionGameObject;
        public GameObject IsLoadingDataRoot;
        public GameObject LoadingErrorRoot;

        private Action<NftItemView> onButtonClickedAction;

        public async void SetData(Nft nft, Action<NftItemView> onButtonClicked)
        {
            if (nft == null)
            {
                return;
            }
            
            CurrentSolPlayNft = nft;
            Icon.gameObject.SetActive(false);
            LoadingErrorRoot.gameObject.SetActive(false);
            IsLoadingDataRoot.gameObject.SetActive(true);

            IsLoadingDataRoot.gameObject.SetActive(false);

            if (nft.metaplexData.nftImage != null)
            {
                Icon.gameObject.SetActive(true);
                Icon.texture = nft.metaplexData.nftImage.file;
            }

            var nftService = ServiceFactory.Resolve<NftService>();
            
            SelectionGameObject.gameObject.SetActive(nftService.IsNftSelected(nft));
            
            if (nft.metaplexData.data.offchainData != null)
            {
                Description.text = nft.metaplexData.data.offchainData.description;
                Headline.text = nft.metaplexData.data.offchainData.name;
            }
            
            Button.onClick.AddListener(OnButtonClicked);
            onButtonClickedAction = onButtonClicked;

            if (ScoreText != null)
            {
                await SetScoreFromPlayerData(nft);
            }
        }

        private async Task SetScoreFromPlayerData(Nft nft)
        {
            if (ScoreText == null)
            {
                return;
            }
            ScoreText.text = "Empty";

            PublicKey.TryFindProgramAddress(new[]
                {
                    Encoding.UTF8.GetBytes("player7"), Web3.Account.PublicKey.KeyBytes,
                    new PublicKey(nft.metaplexData.data.mint).KeyBytes
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

            if (playerData != null)
            {
                if (playerData.ParsedResult != null)
                {
                    Debug.Log("got player data");
                    ScoreText.text = playerData.ParsedResult.Score.ToString();
                }
                else
                {
                    Debug.LogError("Player data parsed result was null " + playerData.OriginalRequest.RawRpcResponse);
                }
            }
        }

        private void OnButtonClicked()
        {
            onButtonClickedAction?.Invoke(this);
        }
    }
}