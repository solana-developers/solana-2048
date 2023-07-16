using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Frictionless;
using Lumberjack;
using Lumberjack.Accounts;
using Lumberjack.Program;
using Solana.Unity.Programs;
using Solana.Unity.Programs.Models;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using SolPlay.Scripts.Services;
using UnityEngine;


    public class Solana2048Service : MonoBehaviour
    {
        public PublicKey Solana_2048_ProgramIdPubKey = new PublicKey("6oKZFmFvcb69ThDuZjrsHABn4A6GMUpPGWNhxJKazWVB");

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
        private bool _isInitialized;
        private LumberjackClient solana_2048_client;
        private int blockBump;

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
                    await InitGameDataAccount(new PublicKey(message.NewNFt.metaplexData.data.mint));
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

            ServiceFactory.Resolve<SolPlayWebSocketService>().Connect("wss://broken-empty-reel.solana-devnet.quiknode.pro/333a00f389fe630f4d331dd740b3aa6b040f8598/");

            solana_2048_client = new LumberjackClient(Web3.Rpc, Web3.WsRpc, Solana_2048_ProgramIdPubKey);
            
            await SubscribeToPlayerDataUpdates();

            sessionWallet = await SessionWallet.GetSessionWallet(Solana_2048_ProgramIdPubKey, "ingame2");
            OnInitialDataLoaded?.Invoke();
            _isInitialized = true;
            
            MessageRouter.AddHandler<SocketServerConnectedMessage>(OnSocketConnectedMessage);
        }

        private void OnSocketConnectedMessage(SocketServerConnectedMessage obj)
        {
            SubscribeToPlayerAccountViaSocket();
        }

        private void SubscribeToPlayerAccountViaSocket()
        {

            ServiceFactory.Resolve<SolPlayWebSocketService>().SubscribeToPubKeyData(PlayerDataPDA, result =>
            {
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
                Debug.LogError("Probably playerData not available " + e.Message);
            }

            if (playerData != null)
            {
                if (playerData.WasSuccessful && playerData.ParsedResult != null)
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
                
                if (!playerData.WasSuccessful)
                {
                    await UniTask.Delay(500);
                    SubscribeToPlayerDataUpdates();
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

        public async UniTask<RequestResult<string>> InitGameDataAccount(PublicKey avatar)
        {
            var tx = new Transaction()
            {
                FeePayer = Web3.Account,
                Instructions = new List<TransactionInstruction>(),
                RecentBlockHash = await Web3.BlockHash(Commitment.Confirmed, false)
            };

            InitPlayerAccounts accounts = new InitPlayerAccounts();
            accounts.Player = PlayerDataPDA;
            accounts.Signer = Web3.Account;
            accounts.SystemProgram = SystemProgram.ProgramIdKey;
            accounts.Avatar = avatar;
            accounts.Highscore = HighscorePDA;
            
            var initTx = LumberjackProgram.InitPlayer(accounts, Solana_2048_ProgramIdPubKey);
            tx.Add(initTx);

            if (!(await sessionWallet.IsSessionTokenInitialized()))
            {
                var topUp = true;

                var validity = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds();
                var createSessionIX = sessionWallet.CreateSessionIX(topUp, validity);
                tx.Add(createSessionIX);
                Debug.Log("Has no session -> partial sign");
                tx.PartialSign(new[] { Web3.Account, sessionWallet.Account });
            }

            RequestResult<string> initResult;
            try
            {
                initResult =  await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
            }
            catch (Exception e)
            {
                Debug.LogError("There was an error signing transaction");
                return null;
            }
            Debug.Log("init response: " + initResult.RawRpcResponse);
            if (initResult.WasSuccessful)
            {
                Debug.Log("Confirming");
                await UniTask.Delay(500); 
                Debug.Log("confirmed");
            }
            Debug.Log("subscribe");
            await SubscribeToPlayerDataUpdates();
            Debug.Log("Init result: " + initResult.Result + " raw: " + initResult.RawRpcResponse);
            return initResult;
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
                RecentBlockHash = await Web3.BlockHash(Commitment.Confirmed)
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
                if (!(await sessionWallet.IsSessionTokenInitialized()))
                {
                    var topUp = true;

                    var validity = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds();
                    var createSessionIX = sessionWallet.CreateSessionIX(topUp, validity);
                    accounts.Signer = Web3.Account.PublicKey;
                    tx.Add(createSessionIX);
                    var pushInDirectionInstruction = LumberjackProgram.PushInDirection(accounts, direction, (byte) blockBump ,Solana_2048_ProgramIdPubKey);
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
                    var pushInDirectionInstruction = LumberjackProgram.PushInDirection(accounts, direction, (byte) blockBump ,Solana_2048_ProgramIdPubKey);
                    tx.Add(pushInDirectionInstruction);
                    Debug.Log("Has session -> sign and send session wallet");
                    walletToUse = sessionWallet;
                }
                
                SendAndConfirmTransaction(walletToUse, tx, "Push in direction: " + direction, () => {}, data =>
                {
                    OnGameReset?.Invoke();
                    SubscribeToPlayerDataUpdates();
                });
            }
        }

        // This is just a workaround since the Solana UnitySDK currently the confirm transactions gets stuck in webgl
        // because of Task.Delay(). Should be fixed soon.
        private async void SendAndConfirmTransaction(WalletBase wallet, Transaction transaction, string label = "", Action onSucccess = null, Action<ErrorData> onError = null)
        {
            transactionsInProgress++;
            var res=  await wallet.SignAndSendTransaction(transaction, commitment: Commitment.Confirmed);
            if (res.WasSuccessful && res.Result != null)
            {
                bool done = false;
                bool failed = false;
                int counter = 0;
                while (!done)
                {
                    Task<RequestResult<ResponseValue<List<SignatureStatusInfo>>>> task =
                        wallet.ActiveRpcClient.GetSignatureStatusesAsync(new List<string>() {res.Result}, true);
                    await task;
                    counter++;
                    foreach (var signatureStatusInfo in task.Result.Result.Value)
                    {
                        if (signatureStatusInfo != null && signatureStatusInfo.ConfirmationStatus == "confirmed")
                        {
                            done = true;
                        }
                    }
                    await UniTask.Delay(100);
                    if (counter >= 60)
                    {
                        failed = true;
                        done = true;
                    }
                }

                if (failed)
                {
                    onError?.Invoke(res.ErrorData);
                    Debug.LogError("Transaction failed to confirm.");
                }
                else
                {
                    onSucccess?.Invoke();
                }
                
                //await Web3.Rpc.ConfirmTransaction(res.Result, Commitment.Confirmed);
            }
            else
            {
                Debug.LogError("Transaction failed: " + res.RawRpcResponse);

                onError?.Invoke(res.ErrorData);
            }
            Debug.Log($"Send tranaction {label} with response: {res.RawRpcResponse}");
            transactionsInProgress--;
        }

        public async void RequestHighscore()
        {
            try
            {
                var highscoreData = await solana_2048_client.GetHighscoreAsync(HighscorePDA, Commitment.Confirmed);
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
                RecentBlockHash = await Web3.BlockHash(Commitment.Confirmed, false)
            };
            var selectedNft = ServiceFactory.Resolve<NftService>().SelectedNft;

            RestartAccounts accounts = new RestartAccounts();
            accounts.Player = PlayerDataPDA;
            accounts.SystemProgram = SystemProgram.ProgramIdKey;
            accounts.Avatar = selectedNft == null ? Web3.Account.PublicKey : new PublicKey(selectedNft.metaplexData.data.mint);
            accounts.Highscore = HighscorePDA;
            
            blockBump = (blockBump +1) % 250;

            WalletBase walletToUse = null;
            
            if (!(await sessionWallet.IsSessionTokenInitialized()))
            {
                var topUp = true;

                var validity = DateTimeOffset.UtcNow.AddHours(23).ToUnixTimeSeconds();
                var createSessionIX = sessionWallet.CreateSessionIX(topUp, validity);
                accounts.Signer = Web3.Account.PublicKey;
                tx.Add(createSessionIX);
                blockBump = (blockBump +1) % 250;
                var restartInstruction = LumberjackProgram.Restart(accounts, 0, (byte) blockBump ,Solana_2048_ProgramIdPubKey);
                tx.Add(restartInstruction);
                Debug.Log("Has no session -> partial sign");
                tx.PartialSign(new[] { Web3.Account, sessionWallet.Account });
                walletToUse = Web3.Wallet;
            }
            else
            {
                tx.FeePayer = sessionWallet.Account.PublicKey;
                accounts.SessionToken = sessionWallet.SessionTokenPDA;
                accounts.Signer = sessionWallet.Account.PublicKey;
                var pushInDirectionInstruction = LumberjackProgram.Restart(accounts, 0, (byte) blockBump ,Solana_2048_ProgramIdPubKey);
                tx.Add(pushInDirectionInstruction);
                Debug.Log("Has session -> sign and send session wallet");
                walletToUse = sessionWallet;
            }
            
            SendAndConfirmTransaction(walletToUse, tx, "Reset game", onSucccess: () =>
            {
                OnGameReset?.Invoke();
                SubscribeToPlayerDataUpdates();
            });
        }
    }
