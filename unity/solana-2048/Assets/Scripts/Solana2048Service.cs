using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Frictionless;
using SolanaTwentyfourtyeight;
using SolanaTwentyfourtyeight.Accounts;
using SolanaTwentyfourtyeight.Program;
using Solana.Unity.Programs;
using Solana.Unity.Programs.Models;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using SolPlay.Scripts.Services;
using UnityEngine;
using UnityEngine.AI;


public class Solana2048Service : MonoBehaviour
    {
        public static PublicKey Solana_2048_ProgramIdPubKey = new PublicKey("BTN22dEcBJcDF1vi81x5t3pXtD49GFA4cn3vDDrEyT3r");
        public static PublicKey ClientDevWallet = new PublicKey("GsfNSuZFrT2r4xzSndnCSs9tTXwt47etPqU8yFVnDcXd");

        public static Solana2048Service Instance { get; private set; }
        public static Action<PlayerData> OnPlayerDataChanged;
        public static Action<Highscore> OnHighscoreChanged;
        public static Action OnInitialDataLoaded;
        public static Action OnGameReset;
        public bool IsAnyTransactionInProgress => transactionsInProgress > 0;
        public PlayerData CurrentPlayerData;
        public Highscore CurrentHighscoreData;
        public int transactionsInProgress;

        private SessionWallet sessionWallet;
        private PublicKey PlayerDataPDA;
        private PublicKey HighscorePDA;
        public PublicKey PricePoolPDA;
        private bool _isInitialized;
        private bool _isSessionInitialized;
        public SolanaTwentyfourtyeightClient solana_2048_client;
        private int blockBump;
        private string cachedBlockHash;

        private List<TimeSpan> socketResponseTimes = new List<TimeSpan>();
        private DateTime requestStarted = DateTime.Now;

        private void Awake() 
        {
            if (Instance != null && Instance != this) 
            { 
                Destroy(this); 
            } 
            else 
            { 
                Instance = this; 
            }

            Web3.OnLogin += OnLogin;
        }

        private void Start()
        {
            MessageRouter
                .AddHandler<NftSelectedMessage>(OnNftSelectedMessage);
        }
        
        private async void OnNftSelectedMessage(NftSelectedMessage message)
        {
            OnGameReset?.Invoke();
            CurrentPlayerData = null;

            //ServiceFactory.Resolve<SolPlayWebSocketService>().UnSubscribeFromPubKeyData(PlayerDataPDA, );
            
            PublicKey.TryFindProgramAddress(new[]
                    {Encoding.UTF8.GetBytes("player7"), Web3.Account.PublicKey.KeyBytes, new PublicKey(message.NewNFt.metaplexData.data.mint).KeyBytes},
                Solana_2048_ProgramIdPubKey, out PlayerDataPDA, out byte bump);

            transactionsInProgress++;
            
            try
            {
                var playerDataResult = await solana_2048_client.GetPlayerDataAsync(PlayerDataPDA, Commitment.Confirmed);
                Debug.Log("Got player data parsed result: " + playerDataResult.ParsedResult);
                if (playerDataResult.ParsedResult != null)
                {
                    CurrentPlayerData = playerDataResult.ParsedResult;
                    await SubscribeToPlayerDataUpdates();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("Initializing player data. " + e.Message);
            }

            if (CurrentPlayerData == null)
            {
                try
                {
                    await InitGameDataAccount();
                }
                catch (Exception e)
                {
                    transactionsInProgress--;
                    Debug.LogWarning("Initializing player data. " + e.Message);
                    return;
                }
            }
            transactionsInProgress--;
            Debug.Log("Player data pda: " + PlayerDataPDA);
        }
        
        private void OnDestroy()
        {
            Web3.OnLogin -= OnLogin;
        }

        private async void OnLogin(Account account)
        {
           var solBalance = await Web3.Instance.WalletBase.GetBalance(Commitment.Confirmed);
            if (solBalance < 20000)
            {
                Debug.Log("Not enough sol. Requesting airdrop");
                var result = await Web3.Instance.WalletBase.RequestAirdrop(commitment: Commitment.Confirmed);
                if (!result.WasSuccessful)
                {
                    Debug.Log("Airdrop failed.");
                }
            }
            Debug.Log(account.PublicKey + " logged in");

            PublicKey.TryFindProgramAddress(new[]
                    {Encoding.UTF8.GetBytes("player7"), account.PublicKey.KeyBytes, account.PublicKey.KeyBytes},
                Solana_2048_ProgramIdPubKey, out PlayerDataPDA, out byte bump);
            Debug.Log("Player data pda: " + PlayerDataPDA);

            PublicKey.TryFindProgramAddress(new[]
                    {Encoding.UTF8.GetBytes("highscore_list_v2")},
                Solana_2048_ProgramIdPubKey, out HighscorePDA, out byte bump2);

            PublicKey.TryFindProgramAddress(new[]
                    {Encoding.UTF8.GetBytes("price_pool")},
                Solana_2048_ProgramIdPubKey, out PricePoolPDA, out byte bump3);

            MessageRouter.AddHandler<SocketServerConnectedMessage>(OnSocketConnectedMessage);
            ServiceFactory.Resolve<SolPlayWebSocketService>().Connect(Web3.Wallet.ActiveStreamingRpcClient.NodeAddress.ToString());

            solana_2048_client = new SolanaTwentyfourtyeightClient(Web3.Wallet.ActiveRpcClient, Web3.Wallet.ActiveStreamingRpcClient, Solana_2048_ProgramIdPubKey);

            StartCoroutine(PollRecentBlockhash());
            
            await SubscribeToPlayerDataUpdates();

            sessionWallet = await SessionWallet.GetSessionWallet(Solana_2048_ProgramIdPubKey, "ingame2", 
                RpcCluster.MainNet, Web3.Wallet.ActiveRpcClient.NodeAddress.ToString(), 
                Web3.Wallet.ActiveStreamingRpcClient.NodeAddress.ToString());
            if (sessionWallet != null || Web3.Wallet != null)
            {
                Debug.Log("Session wallet pubkey: " + sessionWallet.Account.PublicKey + " address: " + Web3.Wallet.ActiveRpcClient.NodeAddress);   
            }
            else
            {
                Debug.LogError("Session wallet is not initialized properly. " + sessionWallet + Web3.Wallet);
            }
            _isSessionInitialized = await IsSessionTokenInitialized();
            OnInitialDataLoaded?.Invoke();
            _isInitialized = true;
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus && Web3.Instance.WalletBase != null)
            {
                UpdateRecentBlockHash();
            }
            Debug.Log("Has focus" + focus);
        }
        private IEnumerator PollRecentBlockhash()
        {
            UpdateRecentBlockHash();
            while (true)
            {
                yield return new WaitForSeconds(5);
                UpdateRecentBlockHash();
            }
        }

        private void UpdateRecentBlockHash()
        {
            UpdateRecentBlockHashAsync();
        }
        
        private async void UpdateRecentBlockHashAsync()
        {
            var blockHash = await Web3.Wallet.ActiveRpcClient.GetLatestBlockHashAsync(Commitment.Confirmed);                
            Debug.Log("Request block hash. " + Web3.Wallet.ActiveRpcClient.NodeAddress);    

            if (blockHash.Result != null && blockHash.Result.Value != null)
            {
                cachedBlockHash = blockHash.Result.Value.Blockhash;
                Debug.Log("Cached Blockhash: " + cachedBlockHash);
            }
            else
            {
                Debug.Log("Could not get new block hash. " + blockHash.RawRpcResponse + " request: " + blockHash.RawRpcRequest);    
            }
        }
        
        private void OnSocketConnectedMessage(SocketServerConnectedMessage obj)
        {
            SubscribeToPlayerAccountViaSocket();
        }

        private void PrintAverageResponseTime()
        {
            float average = 0;
            foreach (var timeSpan in socketResponseTimes)
            {
                average += timeSpan.Milliseconds;
            }

            average /= socketResponseTimes.Count;
            Debug.Log("Current average socket response time: " + average);
        }
        
        private void SubscribeToPlayerAccountViaSocket()
        {
            ServiceFactory.Resolve<SolPlayWebSocketService>().SubscribeToPubKeyData(PlayerDataPDA, result =>
            {
                TimeSpan timeBetweenRequestAnResponse = DateTime.Now - requestStarted;
                socketResponseTimes.Insert(0, timeBetweenRequestAnResponse);
                socketResponseTimes = socketResponseTimes.Take(10).ToList();
                PrintAverageResponseTime();
                    
                var playerData = PlayerData.Deserialize(Convert.FromBase64String(result.result.value.data[0]));
                CurrentPlayerData = playerData;
                Debug.Log(
                    $"New game data arrived x: {CurrentPlayerData.NewTileX} y: {CurrentPlayerData.NewTileY} level: {CurrentPlayerData.NewTileLevel}");
                OnPlayerDataChanged?.Invoke(playerData);
                string grid = "";
                for (int i = 0; i < playerData.Board.Data.Length; i++)
                {
                    for (int j = 0; j < playerData.Board.Data[i].Length; j++)
                    {
                        grid += "-" + playerData.Board.Data[i][j].ToString();
                    }
                }

                Debug.Log(grid);
            });
        }

        public bool IsInitialized()
        {
            return _isInitialized;
        }

        public async UniTask SubscribeToPlayerDataUpdates()
        {
            var selectedNFT = ServiceFactory.Resolve<NftService>().SelectedNft;
            PublicKey.TryFindProgramAddress(new[]
                {
                    Encoding.UTF8.GetBytes("player7"), Web3.Account.PublicKey.KeyBytes,
                    selectedNFT == null
                        ? Web3.Account.PublicKey.KeyBytes
                        : new PublicKey(selectedNFT.metaplexData.data.mint).KeyBytes
                },
                Solana_2048_ProgramIdPubKey, out PlayerDataPDA, out byte bump);
            
            AccountResultWrapper<PlayerData> playerData = null;
            transactionsInProgress++;

            try
            {
                playerData = await solana_2048_client.GetPlayerDataAsync(PlayerDataPDA, Commitment.Confirmed);
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
                    CurrentPlayerData = playerData.ParsedResult;
                    OnPlayerDataChanged?.Invoke(playerData.ParsedResult);
                    SubscribeToPlayerAccountViaSocket();
                    
                    //await solana_2048_client.SubscribePlayerDataAsync(PlayerDataPDA, OnRecievedPlayerDataUpdate, Commitment.Processed);
                }
                else
                {
                    Debug.LogError("Player data parsed result was null " + playerData.OriginalRequest.RawRpcResponse);
                }
            }
            transactionsInProgress--;
        }

        // Currently using SolPlaySocketServer because of not working reconnects in WebGL 
        /*private void OnRecievedPlayerDataUpdate(SubscriptionState state, ResponseValue<AccountInfo> value, PlayerData playerData)
        {
            Debug.Log("Socket Message " + state + value + playerData);
            Debug.Log("Board " + playerData.Board.Data + " current data");
            
            string grid = "";
            for (int i = 0; i < playerData.Board.Data.Length; i++)
            {
                for (int j = 0; j < playerData.Board.Data[i].Length; j++)
                {
                    grid += playerData.Board.Data[i][j].ToString();
                }
            }
            Debug.Log(grid);
            CurrentPlayerData = playerData;
            OnPlayerDataChanged?.Invoke(playerData);
        }*/

        public async UniTask<bool> InitGameDataAccount()
        {
            var selectedNft = ServiceFactory.Resolve<NftService>().SelectedNft;
            PublicKey avatar = selectedNft == null
                ? Web3.Account.PublicKey
                : new PublicKey(selectedNft.metaplexData.data.mint);
            var tx = new Transaction()
            {
                FeePayer = Web3.Account,
                Instructions = new List<TransactionInstruction>(),
                RecentBlockHash = cachedBlockHash
            };

            InitPlayerAccounts accounts = new InitPlayerAccounts();
            accounts.Player = PlayerDataPDA;
            accounts.Signer = Web3.Account;
            accounts.SystemProgram = SystemProgram.ProgramIdKey;
            accounts.Avatar = avatar;
            accounts.Highscore = HighscorePDA;
            accounts.PricePool = PricePoolPDA;
            accounts.ClientDevWallet = ClientDevWallet;

            var initTx = SolanaTwentyfourtyeightProgram.InitPlayer(accounts, Solana_2048_ProgramIdPubKey);
            tx.Add(initTx);

            if (!(await IsSessionTokenInitialized()))
            {
                var topUp = true;

                var validity = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds();
                var createSessionIX = sessionWallet.CreateSessionIX(topUp, validity);
                tx.Add(createSessionIX);
                Debug.Log("Has no session -> partial sign");
                tx.PartialSign(new[] { Web3.Account, sessionWallet.Account });
            }

            bool success = await SendAndConfirmTransaction(Web3.Wallet, tx, "initialize");

            if (!success)
            {
                Debug.LogError("Init was not successful");
            }
            
            await SubscribeToPlayerDataUpdates();
            return success;
        }

        // TODO: this is temporary because the session wallet wont be available for another 31 confirmations withoue this workaround
        public async Task<bool> IsSessionTokenInitialized()
        {
            var sessionTokenData = await Web3.Rpc.GetAccountInfoAsync(sessionWallet.SessionTokenPDA, Commitment.Confirmed);
            return sessionTokenData.Result.Value != null;
        }
        
        public async Task<SessionWallet> RevokeSession()
        { 
            sessionWallet.Logout();
            return sessionWallet;
        }

        public async void PushInDirection(bool useSession, byte direction)
        {
            var tx = new Transaction()
            {
                FeePayer = Web3.Account,
                Instructions = new List<TransactionInstruction>(),
                RecentBlockHash = cachedBlockHash
            };

            var selectedNft = ServiceFactory.Resolve<NftService>().SelectedNft;
            
            PushInDirectionAccounts accounts = new PushInDirectionAccounts();
            accounts.Player = PlayerDataPDA;
            accounts.SystemProgram = SystemProgram.ProgramIdKey;
            accounts.Avatar = selectedNft == null ? Web3.Account.PublicKey : new PublicKey(selectedNft.metaplexData.data.mint);
            accounts.Highscore = HighscorePDA;

            blockBump = (blockBump +1) % 250;

            WalletBase walletToUse = null;

            if (useSession)
            {
                if (!_isSessionInitialized)
                {
                    var topUp = true;

                    var validity = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds();
                    var createSessionIX = sessionWallet.CreateSessionIX(topUp, validity);
                    accounts.Signer = Web3.Account.PublicKey;
                    tx.Add(createSessionIX);
                    var pushInDirectionInstruction = SolanaTwentyfourtyeightProgram.PushInDirection(accounts, direction, (byte) blockBump ,Solana_2048_ProgramIdPubKey);
                    tx.Add(pushInDirectionInstruction);
                    Debug.Log("Has no session -> partial sign");
                    tx.PartialSign(new[] { Web3.Account, sessionWallet.Account });
                    walletToUse = Web3.Wallet;
                }
                else
                {
                    tx.FeePayer = sessionWallet.Account.PublicKey;
                    accounts.SessionToken = sessionWallet.SessionTokenPDA;
                    accounts.Signer = sessionWallet.Account.PublicKey;
                    var pushInDirectionInstruction = SolanaTwentyfourtyeightProgram.PushInDirection(accounts, direction, (byte) blockBump ,Solana_2048_ProgramIdPubKey);
                    tx.Add(pushInDirectionInstruction);
                    Debug.Log("Has session -> sign and send session wallet");
                    walletToUse = sessionWallet;
                }
                
                requestStarted = DateTime.Now;
                SendAndConfirmTransaction(walletToUse, tx, "Push in direction: " + direction, () => {}, data =>
                {
                    OnGameReset?.Invoke();
                    SubscribeToPlayerDataUpdates();
                });
                
                _isSessionInitialized = await IsSessionTokenInitialized();
            }
        }

        // This is just a workaround since the Solana UnitySDK currently the confirm transactions gets stuck in webgl
        // because of Task.Delay(). Should be fixed soon.
        private async Task<bool> SendAndConfirmTransaction(WalletBase wallet, Transaction transaction, string label = "", Action onSucccess = null, Action<ErrorData> onError = null)
        {
            transactionsInProgress++;
            var res=  await wallet.SignAndSendTransaction(transaction, commitment: Commitment.Confirmed);
            Debug.Log("Transaction sent: " + res.RawRpcResponse);
            if (res.WasSuccessful && res.Result != null)
            {
                await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
            }
            else
            {
                Debug.LogError("Transaction failed: " + res.RawRpcResponse);

                onError?.Invoke(res.ErrorData);
            }
            Debug.Log($"Send tranaction {label} with response: {res.RawRpcResponse}");
            transactionsInProgress--;
            onSucccess?.Invoke();
            return true;
        }

        public async void RequestHighscore()
        {
            try
            {
                AccountResultWrapper<Highscore> highscoreData = await solana_2048_client.GetHighscoreAsync(HighscorePDA, Commitment.Confirmed);
                if (highscoreData.ParsedResult != null)
                {
                    CurrentHighscoreData = highscoreData.ParsedResult;
                    OnHighscoreChanged?.Invoke(highscoreData.ParsedResult);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Probably highscore data not available " + e.Message);
            }
        }

        public async void ResetGame()
        {
            var tx = new Transaction()
            {
                FeePayer = Web3.Account,
                Instructions = new List<TransactionInstruction>(),
                RecentBlockHash = cachedBlockHash
            };
            var selectedNft = ServiceFactory.Resolve<NftService>().SelectedNft;

            RestartAccounts accounts = new RestartAccounts();
            accounts.Player = PlayerDataPDA;
            accounts.SystemProgram = SystemProgram.ProgramIdKey;
            accounts.Avatar = selectedNft == null ? Web3.Account.PublicKey : new PublicKey(selectedNft.metaplexData.data.mint);
            accounts.Highscore = HighscorePDA;
            accounts.PricePool = PricePoolPDA;
            accounts.ClientDevWallet = ClientDevWallet;
            
            tx.FeePayer = Web3.Wallet.Account.PublicKey;
            accounts.Signer = Web3.Wallet.Account.PublicKey;
            var pushInDirectionInstruction = SolanaTwentyfourtyeightProgram.Restart(accounts,Solana_2048_ProgramIdPubKey);
            tx.Add(pushInDirectionInstruction);
            
            SendAndConfirmTransaction(Web3.Wallet, tx, "Reset game", onSucccess: () =>
            {
                OnGameReset?.Invoke();
                SubscribeToPlayerDataUpdates();
            });
        }
    }
