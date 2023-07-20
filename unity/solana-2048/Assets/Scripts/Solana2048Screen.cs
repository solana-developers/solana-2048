using System;
using System.Collections;
using Frictionless;
using SolanaTwentyfourtyeight.Accounts;
using Solana.Unity.SDK;
using Solana.Unity.Wallet.Bip39;
using SolPlay.Scripts.Services;
using SolPlay.Scripts.Ui;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class Solana2048Screen : MonoBehaviour
{
    public Button LoginButton;
    public Button LoginWalletAdapterDevnetButton;
    public Button LoginWalletAdapterMainNetButton;
    public Button ResetButton;
    public Button TestButton;
    
    public Button RevokeSessionButton;
    public Button NftsButton;
    public Button HighscoreButton;
    public Button InitGameDataButton;

    public TextMeshProUGUI ScoreText;

    public GameObject LoadingSpinner;

    public GameObject LoggedInRoot;
    public GameObject NotInitializedRoot;
    public GameObject InitializedRoot;
    public GameObject NotLoggedInRoot;

    public string DevnetRpc = "";
    public string MainnetRpc = "";
    public string EditortRpc = "";
    private uint targetScore;
    private uint currentScore;
    
    void Start()
    {
        LoggedInRoot.SetActive(false);
        NotLoggedInRoot.SetActive(true);
        
        LoginButton.onClick.AddListener(OnEditorLoginClicked);
        LoginButton.gameObject.SetActive(Application.isEditor);
        LoginWalletAdapterDevnetButton.onClick.AddListener(OnLoginWalletAdapterButtonClicked);
        LoginWalletAdapterMainNetButton.onClick.AddListener(OnLoginWalletAdapterMainnetButtonClicked);
        RevokeSessionButton.onClick.AddListener(OnRevokeSessionButtonClicked);
        NftsButton.onClick.AddListener(OnNftsButtonClicked);
        HighscoreButton.onClick.AddListener(OnHighscoreButtonClicked);
        ResetButton.onClick.AddListener(OnResetButtonClicked);
        InitGameDataButton.onClick.AddListener(OnInitGameDataButtonClicked);
        TestButton.onClick.AddListener(OnTestClicked);
        Solana2048Service.OnPlayerDataChanged += OnPlayerDataChanged;

        StartCoroutine(UpdateNextEnergy());
        
        Solana2048Service.OnInitialDataLoaded += UpdateContent;
    }

    private void OnTestClicked()
    {
        ServiceFactory.Resolve<BoardManager>().SpawnTestTiles();
    }

    private void Update()
    {
        LoadingSpinner.gameObject.SetActive(Solana2048Service.Instance.IsAnyTransactionInProgress);
        InitGameDataButton.interactable = !Solana2048Service.Instance.IsAnyTransactionInProgress;
        if (targetScore != currentScore)
        {
            currentScore = (uint) Mathf.Lerp(currentScore, targetScore, Time.deltaTime);
            ScoreText.text = currentScore.ToString();
        }
    }

    private async void OnInitGameDataButtonClicked()
    {
        await Solana2048Service.Instance.InitGameDataAccount();
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
        await Web3.Instance.LoginWalletAdapter();
    }

    private async void OnLoginWalletAdapterMainnetButtonClicked()
    {
        Web3.Instance.rpcCluster = RpcCluster.MainNet;
        Web3.Instance.customRpc = MainnetRpc;
        Web3.Instance.webSocketsRpc = MainnetRpc.Replace("https://", "wss://");

        await Web3.Instance.LoginWalletAdapter();
    }

    private async void OnEditorLoginClicked()
    {
        Web3.Instance.rpcCluster = RpcCluster.DevNet;
        Web3.Instance.customRpc = EditortRpc;
        Web3.Instance.webSocketsRpc = EditortRpc.Replace("https://", "wss://");

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
    }

    private void UpdateContent()
    {
        var isInitialized = Solana2048Service.Instance.IsInitialized();
        LoggedInRoot.SetActive(Web3.Account != null);
        NotInitializedRoot.SetActive(!isInitialized || Solana2048Service.Instance.CurrentPlayerData == null);
        InitGameDataButton.gameObject.SetActive(isInitialized && Solana2048Service.Instance.CurrentPlayerData == null);
        InitializedRoot.SetActive(Solana2048Service.Instance.CurrentPlayerData != null);

        NotLoggedInRoot.SetActive(Web3.Account == null);

        if (Solana2048Service.Instance.CurrentPlayerData == null)
        {
            return;
        }
    
        targetScore = Solana2048Service.Instance.CurrentPlayerData.Score;
    }

}
