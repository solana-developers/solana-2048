using System.Threading.Tasks;
using Frictionless;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using SolPlay.Scripts.Services;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WebSocketSharp;

namespace SolPlay.Scripts.Ui
{
    /// <summary>
    /// Screen that loads all NFTs when opened
    /// </summary>
    public class NftListPopup : BasePopup
    {
        public Button GetNFtsDataButton;
        public Button MintInAppButton;
        public NftItemListView NftItemListView;
        public GameObject YouDontOwnANftOfCollectionRoot;
        public GameObject YouOwnANftOfCollectionRoot;
        public GameObject LoadingSpinner;
        public GameObject MinitingBlocker;
        public TMP_InputField NftNameInput;

        private bool loadedNfts;
        
        void Start()
        {
            GetNFtsDataButton.onClick.AddListener(OnGetNftButtonClicked);
            MintInAppButton.onClick.AddListener(OnMintInAppButtonClicked);

            MessageRouter
                .AddHandler<NftLoadingStartedMessage>(OnNftLoadingStartedMessage);
            MessageRouter
                .AddHandler<NftLoadingFinishedMessage>(OnNftLoadingFinishedMessage);
            MessageRouter
                .AddHandler<NftLoadedMessage>(OnNftLoadedMessage);
            MessageRouter
                .AddHandler<NftSelectedMessage>(OnNftSelectedMessage);
        }

        private void OnNftSelectedMessage(NftSelectedMessage obj)
        {
            Close();
        }

        public override void Open(UiService.UiData uiData)
        {
            var nftListPopupUiData = (uiData as NftListPopupUiData);

            if (nftListPopupUiData == null)
            {
                Debug.LogError("Wrong ui data for nft list popup");
                return;
            }

            if (!loadedNfts)
            {
                loadedNfts = true;
                ServiceFactory.Resolve<NftService>().LoadNfts();   
            }

            NftItemListView.UpdateContent();
            NftItemListView.SetData(nft =>
            {
                // when an nft was selected we want to close the popup so we can start the game.
                Close();
            });
            base.Open(uiData);
        }

        
        private static string[] firstParts = new string[]
        {
            "Black", "Red", "One-Eyed", "Long", "Mad", "Iron"
        };

        private static string[] middleParts = new string[]
        {
            "Beard", "Hand", "Hook", "Leg", "Tooth", "Sea"
        };

        private static string[] lastParts = new string[]
        {
            "Jack", "Roger", "Sparrow", "Flint", "Morgan", "Barbarossa"
        };

        public static string GeneratePirateName()
        {
            string firstPart = firstParts[Random.Range(0, firstParts.Length)];
            string middlePart = middleParts[Random.Range(0, middleParts.Length)];
            string lastPart = lastParts[Random.Range(0, lastParts.Length)];

            return $"{firstPart} {middlePart} {lastPart}";
        }
        
        private async void OnMintInAppButtonClicked()
        {
            MinitingBlocker.gameObject.SetActive(true);

            string nftNameINput = NftNameInput.text;
            string nftName = nftNameINput.IsNullOrEmpty() || nftNameINput == "Nft Name" ? GeneratePirateName() : nftNameINput;
            
            // Mint a pirate sship
            var signature = await ServiceFactory.Resolve<NftMintingService>()
                .MintNftWithMetaData(
                    "https://shdw-drive.genesysgo.net/QZNGUVnJgkw6sGQddwZVZkhyUWSUXAjXF9HQAjiVZ55/DummyPirateShipMetaData.json",
                    nftName, "Pirate", b =>
                    {
                        if (MinitingBlocker != null)
                        {
                            MinitingBlocker.gameObject.SetActive(false);
                        }

                        ServiceFactory.Resolve<NftService>().LoadNfts();
                    });
            await Web3.Wallet.ActiveRpcClient.ConfirmTransaction(signature, Commitment.Confirmed);
            MinitingBlocker.gameObject.SetActive(false);

            Debug.Log("Mint signature: " + signature);
        }

        private void OnNftLoadedMessage(NftLoadedMessage message)
        {
            NftItemListView.AddNFt(message.Nft);
            UpdateOwnCollectionStatus();
        }

        private bool UpdateOwnCollectionStatus()
        {
            var nftService = ServiceFactory.Resolve<NftService>();
            bool ownsBeaver = nftService.OwnsNftOfMintAuthority(NftService.NftMintAuthority);
            YouDontOwnANftOfCollectionRoot.gameObject.SetActive(!ownsBeaver);
            YouOwnANftOfCollectionRoot.gameObject.SetActive(ownsBeaver);
            return ownsBeaver;
        }

        private void OnGetNftButtonClicked()
        {
            ServiceFactory.Resolve<NftService>().LoadNfts();
        }

        private void OnNftLoadingStartedMessage(NftLoadingStartedMessage message)
        {
            GetNFtsDataButton.interactable = false;
        }

        private void OnNftLoadingFinishedMessage(NftLoadingFinishedMessage message)
        {
            NftItemListView.UpdateContent();
        }

        private void Update()
        {
            var nftService = ServiceFactory.Resolve<NftService>();
            if (nftService != null)
            {
                GetNFtsDataButton.interactable = !nftService.IsLoadingTokenAccounts;
                LoadingSpinner.gameObject.SetActive(nftService.IsLoadingTokenAccounts);
            }
        }
    }
}