using System.Collections;
using DG.Tweening;
using Frictionless;
using SolanaTwentyfourtyeight.Accounts;
using Solana.Unity.SDK;
using Solana.Unity.Wallet;
using Solana.Unity.Wallet.Bip39;
using SolPlay.Scripts.Services;
using SolPlay.Scripts.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Solana2048Screen : MonoBehaviour
{
    public Button LoginButton;
    public Button LoginWalletAdapterDevnetButton;
    public Button LoginWalletAdapterMainNetButton;
    public Button ResetButton;
    public Button TestButton;
    public Button ReloadButton;
    
    public Button RevokeSessionButton;
    public Button NftsButton;
    public Button HighscoreButton;
    public Button InitGameDataButton;

    public TextMeshProUGUI ScoreText;
    public TextMeshProUGUI JackpotText;

    public GameObject LoadingSpinner;

    public GameObject LoggedInRoot;
    public GameObject NotInitializedRoot;
    public GameObject InitializedRoot;
    public GameObject NotLoggedInRoot;
    public bool UseWhirligig;

    public string DevnetRpc = "";
    public string MainnetRpc = "";
    public string EditortRpc = "";
    private uint targetScore;
    private uint currentScore;
    
    async void Start()
    {
        LoggedInRoot.SetActive(false);
        NotLoggedInRoot.SetActive(true);
        
        LoginButton.onClick.AddListener(OnEditorLoginClicked);
        //LoginButton.gameObject.SetActive(Application.isEditor);
        LoginButton.gameObject.SetActive(true);
        LoginWalletAdapterDevnetButton.onClick.AddListener(OnLoginWalletAdapterButtonClicked);
        LoginWalletAdapterMainNetButton.onClick.AddListener(OnLoginWalletAdapterMainnetButtonClicked);
        RevokeSessionButton.onClick.AddListener(OnRevokeSessionButtonClicked);
        NftsButton.onClick.AddListener(OnNftsButtonClicked);
        HighscoreButton.onClick.AddListener(OnHighscoreButtonClicked);
        ResetButton.onClick.AddListener(OnResetButtonClicked);
        InitGameDataButton.onClick.AddListener(OnInitGameDataButtonClicked);
        TestButton.onClick.AddListener(OnTestClicked);
        ReloadButton.onClick.AddListener(OnReloadClicked);
        Solana2048Service.OnPlayerDataChanged += OnPlayerDataChanged;

        StartCoroutine(UpdateNextEnergy());
        
        Solana2048Service.OnInitialDataLoaded += UpdateContent;
        Solana2048Service.OnPricePoolChanged += OnPricePoolChanged;

        Web3.OnLogin += OnLogin;
    }

    private void OnReloadClicked()
    {
        Application.ExternalEval("document.location.reload(true)");
    }

    private async void OnLogin(Account obj)
    {
        if (Solana2048Service.Instance.CurrentPlayerData == null)
        {
           // ServiceFactory.Resolve<UiService>().OpenPopup(UiService.ScreenType.NftListPopup, new NftListPopupUiData(false, Web3.Wallet));
        }
        
        var res = await Web3.Wallet.GetBalance(Solana2048Service.Instance.PricePoolPDA);
        JackpotText.text = res.ToString("F3");
    }

    private void OnPricePoolChanged(string newPricePool)
    {
        JackpotText.text = newPricePool;
    }

    private void OnTestClicked()
    {
        ServiceFactory.Resolve<BoardManager>().SpawnTestTiles();
    }

    private void Update()
    {
        LoadingSpinner.gameObject.SetActive(Solana2048Service.Instance.IsAnyTransactionInProgress);
        // Exception handling does not work when canceling transactions so better have it always enabled.
        //InitGameDataButton.interactable = !Solana2048Service.Instance.IsAnyTransactionInProgress;
        InitGameDataButton.interactable = true;
        ReloadButton.gameObject.SetActive(Solana2048Service.Instance.CantLoadBlockhash);
    }

    private async void OnInitGameDataButtonClicked()
    {
        bool success = await Solana2048Service.Instance.InitGameDataAccount(onError: s =>
        {
            Debug.LogError("Login error: " + s);
        });
    }

    private void OnNftsButtonClicked()
    {
        ServiceFactory.Resolve<UiService>().OpenPopup(UiService.ScreenType.NftListPopup, new NftListPopupUiData(false, Web3.Wallet));
    }
    
    private void OnHighscoreButtonClicked()
    {
        ServiceFactory.Resolve<UiService>().OpenPopup(UiService.ScreenType.HighscorePopup, new UiService.UiData());
    }
    
    private void OnResetButtonClicked()
    {
        Solana2048Service.Instance.ResetGame();
    }

    private async void OnRevokeSessionButtonClicked()
    {
        var res =  await Solana2048Service.Instance.RevokeSession();
        Debug.Log("Revoked Session: " + res.Account);
    }

    private async void OnLoginWalletAdapterButtonClicked()
    {
        Web3.Instance.rpcCluster = RpcCluster.DevNet;
        Web3.Instance.customRpc = DevnetRpc;
        Web3.Instance.webSocketsRpc = DevnetRpc.Replace("https://", "wss://");
        if (UseWhirligig)
        {
            Web3.Instance.webSocketsRpc += "/whirligig/";
        }
        await Web3.Instance.LoginWalletAdapter();
    }

    private async void OnLoginWalletAdapterMainnetButtonClicked()
    {
        Web3.Instance.rpcCluster = RpcCluster.MainNet;
        Web3.Instance.customRpc = MainnetRpc;
        Web3.Instance.webSocketsRpc = MainnetRpc.Replace("https://", "wss://");
        if (UseWhirligig)
        {
            Web3.Instance.webSocketsRpc += "/whirligig/";
        }
        await Web3.Instance.LoginWalletAdapter();
    }

    private async void OnEditorLoginClicked()
    {
        Web3.Instance.rpcCluster = RpcCluster.DevNet;
        Web3.Instance.customRpc = EditortRpc;
        Web3.Instance.webSocketsRpc = EditortRpc.Replace("https://", "wss://");
        
        if (UseWhirligig)
        {
            Web3.Instance.webSocketsRpc += "/whirligig/";
        }
        Debug.Log(Web3.Instance.webSocketsRpc);

        var newMnemonic = new Mnemonic(WordList.English, WordCount.Twelve);

        // Dont use this one for production.
        var account = await Web3.Instance.LoginInGameWallet("1234") ??
                      await Web3.Instance.CreateAccount(newMnemonic.ToString(), "1234");
    }
    
    private IEnumerator UpdateNextEnergy()
    {
        while (true)
        {
            yield return new WaitForSeconds(1);
            UpdateContent();
        }
    }

    private void OnPlayerDataChanged(PlayerData playerData)
    {
        UpdateContent();
                
        DOTween.To(() => currentScore, x => currentScore = x, playerData.Score, 1)
            .OnUpdate(() =>
            {
                ScoreText.text = currentScore.ToString();
            });
        if (currentScore != targetScore)
        {
            ScoreText.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0.1f), 0.3f);   
        }
    }

    private void UpdateContent()
    {
        var isInitialized = Solana2048Service.Instance.IsInitialized();
        LoggedInRoot.SetActive(Web3.Account != null);
        NotInitializedRoot.SetActive(!isInitialized || Solana2048Service.Instance.CurrentPlayerData == null);
        InitGameDataButton.gameObject.SetActive(isInitialized && Solana2048Service.Instance.CurrentPlayerData == null);
        InitializedRoot.SetActive(Solana2048Service.Instance.CurrentPlayerData != null);

        NotLoggedInRoot.SetActive(Web3.Account == null);
    }
}
