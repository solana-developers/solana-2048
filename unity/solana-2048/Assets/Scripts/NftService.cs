using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Frictionless;
using Solana.Unity.Metaplex.NFT.Library;
using Solana.Unity.Metaplex.Utilities.Json;
using Solana.Unity.Programs.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.SDK.Nft;
using Solana.Unity.Wallet;
using SolanaTwentyfourtyeight.Accounts;
using UnityEngine;
using Creator = Solana.Unity.Metaplex.NFT.Library.Creator;

namespace SolPlay.Scripts.Services
{
    /// <summary>
    /// Handles all logic related to NFTs and calculating their power level or whatever you like to do with the NFTs
    /// </summary>
    public class NftService : MonoBehaviour, IMultiSceneSingleton
    {
        public List<Nft> LoadedNfts = new ();
        public bool IsLoadingTokenAccounts { get; private set; }
        public const string NftMintAuthority = "GsfNSuZFrT2r4xzSndnCSs9tTXwt47etPqU8yFVnDcXd";
        public Nft SelectedNft { get; private set; }
        public Texture2D LocalDummyNft;
        public bool LoadNftsOnStartUp = true;
        public bool AddDummyNft = true;
        private Dictionary<string, PlayerData> NftPlayerDatas = new Dictionary<string, PlayerData>();

        public void Awake()
        {
            if (ServiceFactory.Resolve<NftService>() != null)
            {
                Destroy(gameObject);
                return;
            }

            ServiceFactory.RegisterSingleton(this);
            Web3.OnLogin += OnLogin;
        }

        private void OnLogin(Account obj)
        {
            if (!LoadNftsOnStartUp)
            {
                return;
            }

            LoadNfts();
        }

        public void LoadNfts()
        {
            LoadedNfts.Clear();
            Web3.AutoLoadNfts = false;
            Web3.LoadNftsTextureByDefault = false;
            Web3.LoadNFTs(false, true, 500, Commitment.Confirmed).Forget();
            IsLoadingTokenAccounts = true;
            if (AddDummyNft)
            {
                var dummyLocalNft = CreateDummyLocalNft(Web3.Account.PublicKey, Web3.Account.PublicKey);
                LoadedNfts.Add(dummyLocalNft);
                MessageRouter.RaiseMessage(new NftLoadedMessage(dummyLocalNft));
                SelectedNft = dummyLocalNft;
                PlayerPrefs.SetString("SelectedNft", SelectedNft.metaplexData.data.mint);
            }
            Web3.OnNFTsUpdate += (nfts, totalAmount) =>
            {
                foreach (var newNft in nfts)
                {
                    if (newNft.metaplexData == null || newNft.metaplexData.data == null)
                    {
                        continue;
                    }
                    if (GetSelectedNftPubKey() == newNft.metaplexData.data.mint)
                    {
                        //SelectedNft = newNft;
                        //SelectNft(SelectedNft);
                    }
                    
                    bool wasAlreadyLoaded = false;
                    foreach (var oldNft in LoadedNfts)
                    {
                        if (newNft.metaplexData.data.mint == oldNft.metaplexData.data.mint)
                        {
                            wasAlreadyLoaded = true;
                        }
                    }

                    if (!wasAlreadyLoaded)
                    {
                        LoadScoreForNFt(newNft);

                    }
                }

                IsLoadingTokenAccounts = nfts.Count != totalAmount;
            };
        }

        private async Task LoadScoreForNFt(Nft newNft)
        {
            Debug.Log("Load score for: " + newNft.metaplexData.data.mint);
            PublicKey.TryFindProgramAddress(new[]
                {
                    Encoding.UTF8.GetBytes("player7"), Web3.Account.PublicKey.KeyBytes,
                    new PublicKey(newNft.metaplexData.data.mint).KeyBytes
                },
                Solana2048Service.Solana_2048_ProgramIdPubKey, out PublicKey nftPDA, out byte bump);

            AccountResultWrapper<PlayerData> playerData = null;

            try
            {
                playerData =
                    await Solana2048Service.Instance.solana_2048_client.GetPlayerDataAsync(nftPDA, Commitment.Confirmed);
            }
            catch (Exception e)
            {
                //Debug.LogWarning("Could not get player data: " + e);
            }

            if (playerData != null && playerData.ParsedResult != null)
            {
                NftPlayerDatas[newNft.metaplexData.data.mint] = playerData.ParsedResult;
            }
            
            MessageRouter.RaiseMessage(new NftLoadedMessage(newNft));
            LoadedNfts.Add(newNft);
        }

        public Nft CreateDummyLocalNft(string mint, PublicKey owner)
        {
            Nft dummyLocalNft = new Nft();

            var constructor = typeof(MetadataAccount).GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance,
                null, new Type[0], null);
            MetadataAccount metaPlexData = (MetadataAccount) constructor.Invoke(null);

            metaPlexData.offchainData = new MetaplexTokenStandard();
            metaPlexData.offchainData.symbol = "dummy";
            metaPlexData.offchainData.name = "Dummy Nft";
            metaPlexData.offchainData.description = "A dummy nft which uses the wallet puy key";
            metaPlexData.owner = owner;
            metaPlexData.mint = mint;
            metaPlexData.metadata = new OnChainData("Dummy NFT", 
                "dumm", "test", 0, new List<Creator>(), 0, 0, null,
                null, null, true);

            dummyLocalNft.metaplexData = new Metaplex(metaPlexData);
            dummyLocalNft.metaplexData.nftImage = new NftImage()
            {
                name = "DummyNft",
                file = LocalDummyNft
            };

            return dummyLocalNft;
        }

        public bool IsNftSelected(Nft nft)
        {
            return nft.metaplexData.data.mint == GetSelectedNftPubKey();
        }

        public string GetSelectedNftPubKey()
        {
            return PlayerPrefs.GetString("SelectedNft");
        }

        public bool OwnsNftOfMintAuthority(string authority)
        {
            foreach (var nft in LoadedNfts)
            {
                if (nft.metaplexData.data.updateAuthority != null && nft.metaplexData.data.updateAuthority == authority)
                {
                    return true;
                }
            }

            return false;
        }

        public void SelectNft(Nft nft)
        {
            if (nft == null)
            {
                return;
            }

            SelectedNft = nft;
            PlayerPrefs.SetString("SelectedNft", SelectedNft.metaplexData.data.mint);
            MessageRouter.RaiseMessage(new NftSelectedMessage(SelectedNft));
        }

        public void ResetSelectedNft()
        {
            SelectedNft = null;
            PlayerPrefs.DeleteKey("SelectedNft");
            MessageRouter.RaiseMessage(new NftSelectedMessage(SelectedNft));
        }

        public IEnumerator HandleNewSceneLoaded()
        {
            yield return null;
        }

        public bool TryGetScore(Nft nft, out uint score)
        {
            if (NftPlayerDatas.TryGetValue(nft.metaplexData.data.mint, out PlayerData playerData))
            {
                score = playerData.Score; 
                return true;
            }

            score = 0;
            return false;
        }

        public void UpdateScoreForSelectedNFt(PlayerData newPlayerData)
        {
            if (SelectedNft != null)
            {
                NftPlayerDatas[SelectedNft.metaplexData.data.mint] = newPlayerData;
            }
            else
            {
                NftPlayerDatas[Web3.Account.PublicKey] = newPlayerData;
            }
        }
    }

    public class NftLoadedMessage
    {
        public Nft Nft;

        public NftLoadedMessage(Nft nft)
        {
            Nft = nft;
        }
    }

    public class NftSelectedMessage
    {
        public Nft NewNFt;

        public NftSelectedMessage(Nft newNFt)
        {
            NewNFt = newNFt;
        }
    }

    public class NftLoadingStartedMessage
    {
    }

    public class NftLoadingFinishedMessage
    {
    }

}