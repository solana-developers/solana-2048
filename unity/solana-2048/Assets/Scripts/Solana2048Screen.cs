using System.Collections;
using Frictionless;
using Lumberjack.Accounts;
using Solana.Unity.SDK;
using Solana.Unity.Wallet.Bip39;
using SolPlay.Scripts.Services;
using SolPlay.Scripts.Ui;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class Solana2048Screen : MonoBehaviour
{
    public Button LoginButton;
    public Button LoginWalletAdapterButton;
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
    
    void Start()
    {
        LoggedInRoot.SetActive(false);
        NotLoggedInRoot.SetActive(true);
        
        LoginButton.onClick.AddListener(OnLoginClicked);
        LoginWalletAdapterButton.onClick.AddListener(OnLoginWalletAdapterButtonClicked);
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
    }

    private async void OnInitGameDataButtonClicked()
    {
        await Solana2048Service.Instance.InitGameDataAccount(Web3.Account.PublicKey);
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
        await Web3.Instance.LoginWalletAdapter();
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

        ScoreText.text = Solana2048Service.Instance.CurrentPlayerData.Score.ToString();
    }

    private async void OnLoginClicked()
    {
        var newMnemonic = new Mnemonic(WordList.English, WordCount.Twelve);

        // Dont use this one for production.
        var account = await Web3.Instance.LoginInGameWallet("1234") ??
                      await Web3.Instance.CreateAccount(newMnemonic.ToString(), "1234");
    }
}
