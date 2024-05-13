using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Frictionless;
using SolanaTwentyfourtyeight;
using SolanaTwentyfourtyeight.Accounts;
using SolanaTwentyfourtyeight.Program;
using Solana.Unity.Programs;
using Solana.Unity.Programs.Models;
using Solana.Unity.Rpc;
using Solana.Unity.Rpc.Core.Http;
using Solana.Unity.Rpc.Messages;
using Solana.Unity.Rpc.Models;
using Solana.Unity.Rpc.Types;
using Solana.Unity.SDK;
using Solana.Unity.SessionKeys.GplSession.Accounts;
using Solana.Unity.Wallet;
using SolPlay.DeeplinksNftExample.Utils;
using SolPlay.Scripts.Services;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;

public class Solana2048Service : MonoBehaviour
{
    public static PublicKey Solana_2048_ProgramIdPubKey = new PublicKey("2o48ieM95rmHqMWC5B3tTX4DL7cLm4m1Kuwjay3keQSv");
    public static PublicKey ClientDevWallet = new PublicKey("CYg2vSJdujzEC1E7kHMzB9QhjiPLRdsAa4Js7MkuXfYq");

    public static Solana2048Service Instance { get; private set; }
    public static Action<PlayerData> OnPlayerDataChanged;
    public static Action<Highscore> OnHighscoreChanged;
    public static Action<string> OnPricePoolChanged;
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
    private bool _isSessionIsValid;
    public bool CantLoadBlockhash;
    public double CurrentAverageSocketResponseTime;
    public SolanaTwentyfourtyeightClient solana_2048_client;
    private int blockBump;
    private string cachedBlockHash;
    private long? sessionValidUntil;
    private bool waitingForSession;
    private string currentPricePool;
    
    private List<TimeSpan> socketResponseTimes = new List<TimeSpan>();
    private DateTime requestStarted = DateTime.UtcNow;

    private long GetSessionWalletDuration()
    {
        return DateTimeOffset.UtcNow.AddDays(6).ToUnixTimeSeconds();
        //return DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();   
    }

    private void Awake()
    {
        // Session key program isnt compatible so reflection it is 
        /*Type type = typeof(GplSessionProgram);

        // Get the FieldInfo for the private static readonly field
        FieldInfo fieldInfo = type.GetField("ProgramIdKey", BindingFlags.Public | BindingFlags.Static);
        if (fieldInfo != null)
        {
            // Modify the field value using reflection
            fieldInfo.SetValue(null, new PublicKey("3ao63wcSRNa76bncC2M3KupNtXBFiDyNbgK52VG7dLaE"));

            // Verify that the value has changed
            Debug.Log("Prog" + GplSessionProgram.ProgramIdKey); // Output: NewValue
        }
        else
        {
            Debug.Log("Field not found.");
        }*/
        
        if (Instance != null && Instance != this) 
        { 
            Destroy(this); 
        } 
        else 
        { 
            Instance = this; 
        }

        Web3.OnLogin += OnLogin;
        Web3.OnWebSocketConnect += OnSocketConnect;
    }

    private async void OnSocketConnect()
    {
        Debug.Log("On socket connected");
        //await SubscribeToPlayerDataUpdates();
        SubscribeToPlayerAccountViaSocket();
    }

    private void Start()
    {
        MessageRouter
            .AddHandler<NftSelectedMessage>(OnNftSelectedMessage);
        
        PublicKey.TryFindProgramAddress(new[]
                {Encoding.UTF8.GetBytes("highscore_list_v2")},
            Solana_2048_ProgramIdPubKey, out HighscorePDA, out byte bump2);

        PublicKey.TryFindProgramAddress(new[]
                {Encoding.UTF8.GetBytes("price_pool")},
            Solana_2048_ProgramIdPubKey, out PricePoolPDA, out byte bump3);
    }
    
    private void OnDestroy()
    {
        Web3.OnLogin -= OnLogin;
        //Web3.OnWebSocketConnect -= OnSocketConnect;
    }

    private async void OnLogin(Account account)
    {
        Debug.Log("Logged in with:" + Web3.Instance.customRpc);
        if (Web3.Instance.customRpc.Contains("devnet"))
        {
           var solBalance = await Web3.Instance.WalletBase.GetBalance(Commitment.Confirmed); 
           Debug.Log("Solbalance:" + solBalance);

           if (solBalance < 0.2f)
           {
               StartCoroutine(MagicRequest(Web3.Instance.WalletBase.Account.PublicKey));
               Debug.Log("Not enough sol. Requesting airdrop");
               var result = await Web3.Instance.WalletBase.RequestAirdrop(commitment: Commitment.Confirmed);
               if (!result.WasSuccessful)
               {
                   Debug.Log("Airdrop failed.");
               }
           }
        }
        
        var solBalance_2 = await Web3.Instance.WalletBase.GetBalance(Commitment.Confirmed);

        int counter = 5;
        while (solBalance_2 < 0.2f && counter >0)
        {
            counter--;
            solBalance_2 = await Web3.Instance.WalletBase.GetBalance(Commitment.Confirmed);
            await UniTask.Delay(500);
        }

        Debug.Log(account.PublicKey + " logged in");

        MessageRouter.AddHandler<SocketServerConnectedMessage>(OnSocketConnectedMessage);
        //ServiceFactory.Resolve<SolPlayWebSocketService>().Connect(Web3.Wallet.ActiveStreamingRpcClient.NodeAddress.ToString());
        
        solana_2048_client = new SolanaTwentyfourtyeightClient(Web3.Rpc, Web3.WsRpc, Solana_2048_ProgramIdPubKey);

        StartCoroutine(PollRecentBlockhash());
        
        await SubscribeToPlayerDataUpdates();

        await RefreshSessionWallet();

        if (sessionWallet != null)
        {
            if (sessionWallet.Account == null)
            {
                SessionWallet.Instance = null;
                await RefreshSessionWallet();
                Debug.Log("Session wallet account was null: wrong pdw?");
            }
            else
            {
                if (string.IsNullOrEmpty(sessionWallet.Account.PublicKey))
                {
                    SessionWallet.Instance = null;
                    await RefreshSessionWallet();
                }
                Debug.Log("Session wallet pubkey: " + sessionWallet.Account.PublicKey);    
            }
        }
        else
        {
            Debug.LogError("Session wallet is not initialized properly.");
        }

        if (Web3.Wallet != null)
        {
            Debug.Log("Logged in with: " + Web3.Wallet.ActiveRpcClient.NodeAddress);
        }

        _isSessionInitialized = await IsSessionTokenInitialized();
        _isSessionIsValid = await UpdateSessionValid();
        OnInitialDataLoaded?.Invoke();
        
        Debug.Log("Player data pda: " + PlayerDataPDA);
        try
        {
            await solana_2048_client.GetPlayerDataAsync(PlayerDataPDA, Commitment.Confirmed);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Initializing player data. " + e.Message);
            await InitGameDataAccount();
        }
        
        _isInitialized = true;
    }

    IEnumerator MagicRequest(string address)
    {
        using (UnityWebRequest www = UnityWebRequest.Post("https://faucet.solana.com/api/request", "{ \"walletAddress\": \""+address+"\", \"amount\": 1, \"network\": \"devnet\" }", "application/json"))
        {
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Access-Control-Allow-Origin", "*");
            
            www.SetRequestHeader("Authorization", "Bearer ");
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(www.error);
            }
            else
            {
                Debug.Log("Form upload complete!");
            }
        }
    }
    
    private async Task RefreshSessionWallet()
    {
        sessionWallet = await SessionWallet.GetSessionWallet(Solana_2048_ProgramIdPubKey, "ingame2",
            Web3.Wallet);
    }

    private void OnApplicationFocus(bool focus)
    {
        if (focus && Web3.Instance.WalletBase != null)
        {
            UpdateRecentBlockHash();
            StartCoroutine(GetSessionDelayed());
        }
        Debug.Log("Has focus" + focus);
    }

    private IEnumerator GetSessionDelayed()
    {
        yield return new WaitForSeconds(1);
        UpdateRecentBlockHash();
        yield return new WaitForSeconds(3);
        UpdateRecentBlockHash();
        yield return new WaitForSeconds(6);
        UpdateRecentBlockHash();
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
        var blockHash = await Web3.Rpc.GetLatestBlockHashAsync(Commitment.Confirmed);
        
        if (blockHash.Result != null && blockHash.Result.Value != null && blockHash.WasSuccessful)
        {
            cachedBlockHash = blockHash.Result.Value.Blockhash;
            CantLoadBlockhash = false;
        }
        else
        { 
            // Hack because when there is an exception during NFT loading the CrossHttpClient has a stale network request, which 
            // we can here reset via reflection. Reported to the unity sdk team already.
           // var type = typeof(CrossHttpClient);
           // var field = type.GetField("_currentRequestTask", BindingFlags.NonPublic | BindingFlags.Static);
           // field.SetValue(null, null);

            blockHash = await Web3.Rpc.GetLatestBlockHashAsync(Commitment.Confirmed);
        
            Debug.Log("Get block: success: " +blockHash.WasSuccessful + " was parsed: "+blockHash.WasRequestSuccessfullyHandled + " " + blockHash.RawRpcResponse + " request: " + blockHash.RawRpcRequest + " " + blockHash.ServerErrorCode + "" + blockHash.Reason + " rpc: " +Web3.Rpc.NodeAddress);
            
            if (blockHash.Result != null && blockHash.Result.Value != null && blockHash.WasSuccessful)
            {
                cachedBlockHash = blockHash.Result.Value.Blockhash;
                CantLoadBlockhash = false;
            }
            else
            {
                //Debug.Log("Could not get new block hash. " + blockHash.RawRpcResponse + " request: " + blockHash.RawRpcRequest + " " + blockHash.ServerErrorCode + "" + blockHash.Reason + " rpc: " +Web3.Rpc.NodeAddress);
                CantLoadBlockhash = true;
            }
        }

        if (sessionWallet != null)
        {
            _isSessionInitialized = await IsSessionTokenInitialized();
            _isSessionIsValid = await UpdateSessionValid();
        }
    }
    
    private void OnSocketConnectedMessage(SocketServerConnectedMessage obj)
    {
        Debug.Log("Socket connected");
        SubscribeToPlayerAccountViaSocket();
    }

    private void PrintAverageResponseTime()
    {
        double average = 0;
        foreach (var timeSpan in socketResponseTimes)
        {
            average += timeSpan.TotalMilliseconds;
        }

        average /= socketResponseTimes.Count;
        CurrentAverageSocketResponseTime = average;
        Debug.Log("Current average socket response time: " + average);
    }

    private bool socketSubscribed = false;
    private async void SubscribeToPlayerAccountViaSocket()
    {
        if (socketSubscribed || PlayerDataPDA == null)
        {
            return;
        }

        //socketSubscribed = true;
        await solana_2048_client.SubscribePricepoolAsync(Instance.PricePoolPDA, async (state, value, arg3) =>
        {
            await UniTask.SwitchToMainThread();

            currentPricePool = (value.Value.Lamports / (double) WalletBase.SolLamports ).ToString("F3");
            OnPricePoolChanged(currentPricePool);
        }, Commitment.Processed);

        await solana_2048_client.SubscribePlayerDataAsync(PlayerDataPDA, async (state, value, arg3) =>
        {
            Debug.Log("Socket update game data before main thread");
            await UniTask.SwitchToMainThread();
            Debug.Log("Socket update game data ");
            TimeSpan timeBetweenRequestAnResponse = DateTime.UtcNow - requestStarted;
            socketResponseTimes.Insert(0, timeBetweenRequestAnResponse);
            socketResponseTimes = socketResponseTimes.Take(10).ToList();
            PrintAverageResponseTime();
                
            CurrentPlayerData = arg3;
            ServiceFactory.Resolve<NftService>().UpdateScoreForSelectedNFt(CurrentPlayerData);
            Debug.Log(
                $"New game data arrived x: {CurrentPlayerData.NewTileX} y: {CurrentPlayerData.NewTileY} level: {CurrentPlayerData.NewTileLevel}");
            OnPlayerDataChanged?.Invoke(arg3);
            
            string grid = "";
            for (int i = 0; i < arg3.Board.Data.Length; i++)
            {
                for (int j = 0; j < arg3.Board.Data[i].Length; j++)
                {
                    grid += "-" + arg3.Board.Data[i][j];
                }
            }

            Debug.Log(grid);
        }, Commitment.Processed);
        
        /*ServiceFactory.Resolve<SolPlayWebSocketService>().SubscribeToPubKeyData(Instance.PricePoolPDA,
            result =>
            {
                currentPricePool = (result.result.value.lamports / (double) WalletBase.SolLamports ).ToString("F3");
                OnPricePoolChanged(currentPricePool);
            });*/

       /* ServiceFactory.Resolve<SolPlayWebSocketService>().SubscribeToPubKeyData(PlayerDataPDA, result =>
        {
            TimeSpan timeBetweenRequestAnResponse = DateTime.UtcNow - requestStarted;
            socketResponseTimes.Insert(0, timeBetweenRequestAnResponse);
            socketResponseTimes = socketResponseTimes.Take(10).ToList();
            PrintAverageResponseTime();
                
            var playerData = PlayerData.Deserialize(Convert.FromBase64String(result.result.value.data[0]));
            CurrentPlayerData = playerData;
            ServiceFactory.Resolve<NftService>().UpdateScoreForSelectedNFt(CurrentPlayerData);
            Debug.Log(
                $"New game data arrived x: {CurrentPlayerData.NewTileX} y: {CurrentPlayerData.NewTileY} level: {CurrentPlayerData.NewTileLevel}");
            OnPlayerDataChanged?.Invoke(playerData);
            
            string grid = "";
            for (int i = 0; i < playerData.Board.Data.Length; i++)
            {
                for (int j = 0; j < playerData.Board.Data[i].Length; j++)
                {
                    grid += "-" + playerData.Board.Data[i][j];
                }
            }

            Debug.Log(grid);
        });*/
        
        var res = await Web3.Wallet.GetBalance(Instance.PricePoolPDA);
        currentPricePool = res.ToString("F3");
    }

    public bool IsInitialized()
    {
        return _isInitialized;
    }

    public async Task SubscribeToPlayerDataUpdates()
    {
        var selectedNft = ServiceFactory.Resolve<NftService>().SelectedNft;
        PublicKey.TryFindProgramAddress(new[]
            {
                Encoding.UTF8.GetBytes("player7"), Web3.Account.PublicKey.KeyBytes,
                selectedNft == null
                    ? Web3.Account.PublicKey.KeyBytes
                    : new PublicKey(selectedNft.metaplexData.data.mint).KeyBytes
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
                Debug.Log("Got Player data from SubscribeToWebSocket");
                CurrentPlayerData = playerData.ParsedResult;
                OnPlayerDataChanged?.Invoke(playerData.ParsedResult);
                SubscribeToPlayerAccountViaSocket();
                
                // Currently using the SolPlaySocketServer because there were problems with reconnects in WebGL
                //await solana_2048_client.SubscribePlayerDataAsync(PlayerDataPDA, OnRecievedPlayerDataUpdate, Commitment.Processed);
            }
            else
            {
                Debug.LogError("Player data parsed result was null " + playerData.OriginalRequest.RawRpcResponse);
            }
        }
        transactionsInProgress--;
    }

    public async UniTask<bool> InitGameDataAccount(Action<string> onError = null)
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
        AddPriorityFee(tx);
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

            var createSessionIX = sessionWallet.CreateSessionIX(topUp, GetSessionWalletDuration());
            tx.Add(createSessionIX);
            Debug.Log("Has no session -> partial sign");
            tx.PartialSign(new[] { Web3.Account, sessionWallet.Account });
            waitingForSession = true;
        }

        bool success = await SendAndConfirmTransaction(Web3.Wallet, tx, "initialize", () =>
        {
            waitingForSession = false;
        }, s =>
        {
            waitingForSession = false;
            onError?.Invoke(s);
        });
        _isSessionInitialized = await IsSessionTokenInitialized();
        
        if (!success)
        {
            Debug.LogError("Init was not successful");
        }
        
        await SubscribeToPlayerDataUpdates();
        return success;
    }

    // TODO: this is temporary because the session wallet wont be available for another 31 confirmations without this workaround
    public async Task<bool> IsSessionTokenInitialized()
    {
        var sessionTokenData = await Web3.Rpc.GetAccountInfoAsync(sessionWallet.SessionTokenPDA, Commitment.Confirmed);
        if (sessionTokenData.Result != null && sessionTokenData.Result.Value != null)
        {
            return true;
        }
       
        return false;
    }

    public bool IsSessionValid()
    {
        return sessionValidUntil != null && sessionValidUntil > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public bool IsRequestTimeoutActive()
    {
        return waitingForSession;
    }
    
    public async Task<bool> UpdateSessionValid()
    {
        ResponseValue<AccountInfo> sessionTokenData = (await Web3.Rpc.GetAccountInfoAsync(sessionWallet.SessionTokenPDA, Commitment.Confirmed)).Result;

        if (sessionTokenData == null) return false;
        if (sessionTokenData.Value == null || sessionTokenData.Value.Data[0] == null)
        {
            return false;
        }
        
        var sessionToken = SessionToken.Deserialize(Convert.FromBase64String(sessionTokenData.Value.Data[0]));
        
        Debug.Log("Session token valid until: " + (new DateTime(1970, 1, 1)).AddSeconds(sessionToken.ValidUntil) + " Now: " + DateTimeOffset.UtcNow);
        sessionValidUntil = sessionToken.ValidUntil;
        return sessionToken.ValidUntil > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
    
    public async Task<SessionToken> RequestSessionToken()
    {
        ResponseValue<AccountInfo> sessionTokenData = (await Web3.Rpc.GetAccountInfoAsync(sessionWallet.SessionTokenPDA, Commitment.Confirmed)).Result;

        if (sessionTokenData == null) return null;
        if (sessionTokenData.Value == null || sessionTokenData.Value.Data[0] == null)
        {
            return null;
        }
        
        var sessionToken = SessionToken.Deserialize(Convert.FromBase64String(sessionTokenData.Value.Data[0]));

        return sessionToken;
    }

    public async Task<SessionWallet> RevokeSession()
    {
        await sessionWallet.CloseSession();
        sessionWallet.Logout();
        return sessionWallet;
    }
    
    public async Task<SessionWallet> CreateSession()
    {
        var sessionToken = await Instance.RequestSessionToken();
        if (sessionToken != null)
        {
            await RevokeSession();
        }
        
        var tx = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = await Web3.BlockHash(Commitment.Confirmed, false)
        };
        AddPriorityFee(tx);
        SessionWallet.Instance = null;
        await RefreshSessionWallet();
        var sessionIx = sessionWallet.CreateSessionIX(true, GetSessionWalletDuration());
        tx.Add(sessionIx);
        tx.PartialSign(new[] { Web3.Account, sessionWallet.Account });

        var res = await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);

        Debug.Log("Create session wallet: " + res.RawRpcResponse);
        await Web3.Wallet.ActiveRpcClient.ConfirmTransaction(res.Result, Commitment.Confirmed);

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

        AddPriorityFee(tx);
        
        var selectedNft = ServiceFactory.Resolve<NftService>().SelectedNft;
        
        PushInDirectionAccounts accounts = new PushInDirectionAccounts();
        accounts.Player = PlayerDataPDA;
        accounts.SystemProgram = SystemProgram.ProgramIdKey;
        accounts.Avatar = selectedNft == null ? Web3.Account.PublicKey : new PublicKey(selectedNft.metaplexData.data.mint);
        accounts.Highscore = HighscorePDA;

        blockBump = (blockBump +1) % 250;

        if (useSession)
        {
            tx.FeePayer = sessionWallet.Account.PublicKey;
            accounts.SessionToken = sessionWallet.SessionTokenPDA;
            accounts.Signer = sessionWallet.Account.PublicKey;
            var pushInDirectionInstruction = SolanaTwentyfourtyeightProgram.PushInDirection(accounts, direction, (byte) blockBump ,Solana_2048_ProgramIdPubKey);
            tx.Add(pushInDirectionInstruction);
            Debug.Log("Has session -> sign and send session wallet");
            WalletBase walletToUse = sessionWallet;
          
            requestStarted = DateTime.UtcNow;

            await SendAndConfirmTransaction(walletToUse, tx, "Push in direction: " + direction, () =>
            {
            }, data =>
            {
                OnGameReset?.Invoke();
                SubscribeToPlayerDataUpdates();
            });
        }
    }

    private void AddPriorityFee(Transaction tx)
    {
        tx.Add(ComputeBudgetProgram.SetComputeUnitPrice(10000));
    }
    
    // WIP would be good to combine the logout and the create session TX. Like that the player would not see the high 
    // of session top up + session token account.
    /*public async Task PrepareLogout()
    {
        Debug.Log("Preparing Logout");

        var tx = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = cachedBlockHash
        };

        // Get balance and calculate refund
        var balance = (await sessionWallet.GetBalance(sessionWallet.Account.PublicKey)) * SolanaUtils.SolToLamports;
        var estimatedFees = await Web3.Rpc.GetFeesAsync(Commitment.Confirmed);
        var refund = balance - (estimatedFees.Result.Value.FeeCalculator.LamportsPerSignature * 1) - 5000;
        Debug.Log($"LAMPORTS Balance: {balance}, Refund: {refund}");

        tx.Add(sessionWallet.RevokeSessionIX());
        // Issue Refund
        tx.Add(SystemProgram.Transfer(sessionWallet.Account.PublicKey, Web3.Account.PublicKey, (ulong)refund));
        var rest = await sessionWallet.SignAndSendTransaction(tx);
        Debug.Log("Session refund transaction: " + rest.RawRpcResponse);
        sessionWallet.DeleteSessionWallet();
        sessionWallet.Logout();
        
        SessionWallet.Instance = null;
        await RefreshSessionWallet();
        
        var sessionIx = sessionWallet.CreateSessionIX(true, GetSessionWalletDuration());
        tx.Add(sessionIx);
    }*/

    // This is just a workaround since the Solana UnitySDK currently the confirm transactions gets stuck in webgl
    // because of Task.Delay(). Should be fixed soon.
    private async Task<bool> SendAndConfirmTransaction(WalletBase wallet, Transaction transaction, string label = "", Action onSucccess = null, Action<string> onError = null)
    {
        transactionsInProgress++;
        Debug.Log("Sending and confirming transaction: " + label);
        RequestResult<string> res;
        try
        {
            res = await wallet.SignAndSendTransaction(transaction, commitment: Commitment.Confirmed);
        }
        catch (Exception e)
        {
            Debug.Log("Transaction exception " + e);
            transactionsInProgress--;
            onError?.Invoke(e.ToString());
            return false;
        }
        
        Debug.Log("Transaction sent: " + res.RawRpcResponse);
        if (res.WasSuccessful && res.Result != null)
        {
            Debug.Log("Confirm");

            await ConfirmTransaction(Web3.Rpc, res.Result, Commitment.Confirmed);
            Debug.Log("Confirm done");
        }
        else
        {
            Debug.LogError("Transaction failed: " + res.RawRpcResponse);
            if (res.RawRpcResponse.Contains("InsufficientFundsForRent"))
            {
                Debug.Log("Trigger session top up");
                TriggerTopUpTransaction();
            }
            transactionsInProgress--;
            onError?.Invoke(res.RawRpcResponse);
            return false;
        }
        Debug.Log($"Send transaction {label} with response: {res.RawRpcResponse}");
        transactionsInProgress--;
        onSucccess?.Invoke();
        return true;
    }

    private async void TriggerTopUpTransaction()
    {
        var tx = new Transaction()
        {
            FeePayer = Web3.Account,
            Instructions = new List<TransactionInstruction>(),
            RecentBlockHash = cachedBlockHash
        };
        
        AddPriorityFee(tx);

        var amount = SolanaUtils.SolToLamports / 100;
        Debug.Log($"Lamports to top up wallet: {amount}");
        
        // Issue Refund
        tx.Add(SystemProgram.Transfer(Web3.Account, sessionWallet.Account.PublicKey, (ulong)amount));
        var res = await Web3.Wallet.SignAndSendTransaction(tx, commitment: Commitment.Confirmed);
        await ConfirmTransaction(Web3.Rpc, res.Result, Commitment.Confirmed);
        Debug.Log("Top up was successfull");
    }

    public static async UniTask<bool> ConfirmTransaction(
        IRpcClient rpc,
        string hash,
        Commitment commitment = Commitment.Confirmed)
    {
        TimeSpan delay = commitment == Commitment.Finalized ? TimeSpan.FromSeconds(60.0) : TimeSpan.FromSeconds(30.0);
        CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        CancellationToken cancelToken = cancellationTokenSource.Token;
        cancellationTokenSource.CancelAfter(delay);
        if (commitment == Commitment.Processed)
            commitment = Commitment.Confirmed;
        while (!cancelToken.IsCancellationRequested)
        {
            await UniTask.Delay(50, cancellationToken: cancelToken);
            RequestResult<ResponseValue<List<SignatureStatusInfo>>> signatureStatusesAsync = await rpc.GetSignatureStatusesAsync(new List<string>()
            {
                hash
            }, true);
            if (signatureStatusesAsync.WasSuccessful && signatureStatusesAsync.Result?.Value != null && signatureStatusesAsync.Result.Value.TrueForAll((Predicate<SignatureStatusInfo>) (sgn =>
                {
                    if (sgn == null || sgn.ConfirmationStatus == null)
                        return false;
                    if (sgn.ConfirmationStatus.Equals(commitment.ToString().ToLower()))
                        return true;
                    return commitment.Equals((object) Commitment.Confirmed) && sgn.ConfirmationStatus.Equals(Commitment.Finalized.ToString().ToLower());
                })))
                return true;
        }
        return false;
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
        
        AddPriorityFee(tx);
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
        
        await SendAndConfirmTransaction(Web3.Wallet, tx, "Reset game", onSucccess: () =>
        {
            OnGameReset?.Invoke();
            SubscribeToPlayerDataUpdates();
        });
    }
    
    private async void OnNftSelectedMessage(NftSelectedMessage message)
    {
        OnGameReset?.Invoke();
        CurrentPlayerData = null;
        
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
            transactionsInProgress--;
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
    
}
