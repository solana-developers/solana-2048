using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using DefaultNamespace;
using DG.Tweening;
using Frictionless;
using Solana.Unity.Wallet;
using SolanaTwentyfourtyeight.Accounts;
using SolPlay.Scripts.Services;
using SolPlay.Scripts.Ui;
//using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

public class BoardManager : MonoBehaviour
{
    public const int WIDTH = 4;
    public const int HEIGHT = 4;

    public Tile TilePrefab; 
    public Cell CellPrefab; 
    public Cell[,] AllCells = new Cell[4, 4];
    public List<Tile> tiles = new List<Tile>();
    public TileConfig[] tileConfigs;
    public GameObject GameOverText;
    public TextBlimp3D BlimpPrefab;
    public AudioSource MoveAudioSource;
    public AudioSource MergeAudioSource;
    public AudioSource SpawnAudioSource;
    public AudioClip MoveClip;
    public AudioClip Move2Clip;
    public AudioClip MergeClip;
    public AudioClip SpawnClip;
    
    public bool IsWaiting;
    public DateTime? SocketMessageTimeout = null;
    public MeshRenderer BackgroundMeshRenderer;
    public TouchInputHandler TouchInputHandler;
    public DirectionIndicator DirectionIndicator;
    public TextAsset Asset;
    public int HighestMergedTileThisMove;
    
    private Vector2Int? cachedInput = null;
    private bool isInitialized;
    
    private void Awake()
    {
        ServiceFactory.RegisterSingleton(this);
        GameOverText.gameObject.SetActive(false);
    }

    private void Start()
    {
        Solana2048Service.OnPlayerDataChanged += OnPlayerDataChange;
        Solana2048Service.OnGameReset += OnGameReset;

        // Crete Cells
        for (int i = 0; i < WIDTH; i++)
        {
            for (int j = 0; j < HEIGHT; j++)
            {
                Cell cellInstance = Instantiate(CellPrefab, transform);
                cellInstance.transform.position = new Vector3(1.1f * i, 0, -1.1f * j);
                cellInstance.Init(i, j, null);
                AllCells[j,i] = cellInstance;
            } 
        }
    }

    private void OnDestroy()
    {
        Solana2048Service.OnPlayerDataChanged -= OnPlayerDataChange;
        Solana2048Service.OnGameReset -= OnGameReset;
    }

    public void LoadGameStateFromNft(PublicKey player, PublicKey nft)
    {
        PublicKey.TryFindProgramAddress(new[]
            {
                Encoding.UTF8.GetBytes("player7"), player.KeyBytes,nft.KeyBytes
            },
            Solana2048Service.Solana_2048_ProgramIdPubKey, out PublicKey PlayerDataPDA, out byte bump);
        
        
    }
    
    private void OnGameReset()
    {
        isInitialized = false;
        foreach (Tile tile in tiles)
        {
            tile.transform.DOKill();
            Destroy(tile.gameObject);
        }
        tiles.Clear();
        
        for (int i = 0; i < WIDTH; i++)
        {
            for (int j = 0; j < HEIGHT; j++)
            {
                AllCells[i, j].Tile = null;
            }
        }
    }

    private void OnPlayerDataChange(PlayerData playerData)
    {
        SetData(playerData);
    }

    public void SetData(PlayerData playerData)
    {
        if (!isInitialized)
        {
            CreateStartingTiles(playerData);
            isInitialized = true;
        }
        else
        {
            SpawnNewTile(playerData.NewTileX, playerData.NewTileY, playerData.NewTileLevel);
        }

        bool anyTileOutOfSync = false;
        // Compare tiles: 
        for (int i = 0; i < WIDTH; i++)
        {
            for (int j = 0; j < HEIGHT; j++)
            {
                if (playerData.Board.Data[j][i] != 0 && GetCell(i, j).Tile == null)
                {
                    anyTileOutOfSync = true;
                    Debug.LogWarning("Tiles out of sync.");
                }else 
                if (playerData.Board.Data[j][i] != 0 && playerData.Board.Data[j][i] != GetCell(i, j).Tile.currentConfig.Number)
                {
                    anyTileOutOfSync = true;
                    Debug.LogWarning($"Tiles out of sync. x {i} y {j} from socket: {playerData.Board.Data[j][i]} board: {GetCell(i, j).Tile.currentConfig.Number} ");
                }
            } 
        }

        SetBackgroundImageOfHighestTile();
        
        SocketMessageTimeout = null;
        DirectionIndicator.SetDirection(null);
        if (anyTileOutOfSync)
        {
            RefreshFromPlayerdata(playerData);
            return;
        }
        
        IsWaiting = false;

        GameOverText.gameObject.SetActive(playerData.GameOver);
        if (playerData.GameOver)
        {
            Debug.Log("Game over!!");
        }
    }

    private void SetBackgroundImageOfHighestTile()
    {
        TileConfig highestTileConfig = null;

        foreach (var tile in tiles)
        {
            if (highestTileConfig == null || tile.currentConfig.Number > highestTileConfig.Number)
            {
                highestTileConfig = tile.currentConfig;
            }
        }

        if (highestTileConfig != null && highestTileConfig.BackgroundMaterial != null)
        {
            BackgroundMeshRenderer.material = highestTileConfig.BackgroundMaterial;   
        }
    }
    
    public void RefreshFromPlayerdata(PlayerData playerData)
    {
        OnGameReset();
        CreateStartingTiles(playerData);
        isInitialized = true;
        IsWaiting = false;
        GameOverText.gameObject.SetActive(playerData.GameOver);
    }

    private async void Update()
    {
        // This is just here bcause there is currently no way to figure out if a wallet transaction was rejected and 
        // Then the tasks hang, so to make sure all accounts are initialized we wait a bit...
        if (Solana2048Service.Instance.IsRequestTimeoutActive())
        {
            cachedInput = null;
            TouchInputHandler.InputState = null;
            return;
        }

        TouchInputHandler.OnUpdate();
        if (SocketMessageTimeout != null && SocketMessageTimeout + TimeSpan.FromSeconds(5) < DateTime.UtcNow)
        {
            RefreshFromPlayerdata(Solana2048Service.Instance.CurrentPlayerData);
            cachedInput = null;
            SocketMessageTimeout = null;
            await Solana2048Service.Instance.SubscribeToPlayerDataUpdates();
            return;
        }
        
        if (Solana2048Service.Instance.CurrentPlayerData == null || Solana2048Service.Instance.CurrentPlayerData.GameOver || !Solana2048Service.Instance.IsInitialized() 
            || ServiceFactory.Resolve<UiService>().IsAnyPopupOpen())
        {
            TouchInputHandler.InputState = null;
            cachedInput = null;
            return;
        }
        if (Input.GetKeyUp(KeyCode.RightArrow) || Input.GetKeyUp(KeyCode.D))
        {
            cachedInput = Vector2Int.right;
        }
        if (Input.GetKeyUp(KeyCode.DownArrow) || Input.GetKeyUp(KeyCode.S))
        {
            cachedInput = Vector2Int.down;
        }
        if (Input.GetKeyUp(KeyCode.LeftArrow) || Input.GetKeyUp(KeyCode.A))
        {
            cachedInput = Vector2Int.left;
        }
        if (Input.GetKeyUp(KeyCode.UpArrow) || Input.GetKeyUp(KeyCode.W)) 
        {
            cachedInput = Vector2Int.up;
        }
        if (Solana2048Service.Instance == null || IsWaiting)
        {
            return;
        }

        if (TouchInputHandler.InputState != null)
        {
            cachedInput = TouchInputHandler.InputState;
            TouchInputHandler.InputState = null;
        }
           
        if (!Solana2048Service.Instance.IsSessionValid() && cachedInput != null && UiService.OpenPopups == 0)
        {
            var res = await Solana2048Service.Instance.UpdateSessionValid();

            if (UiService.OpenPopups == 0)
            {
                cachedInput = null;
                ServiceFactory.Resolve<UiService>().OpenPopup(UiService.ScreenType.SessionPopup, new SessionPopupUiData());
            }
            return;
        }
        
        var endValue = Quaternion.Euler(0,0, 0);
        var currentRotation = transform.rotation;
        if (cachedInput == Vector2Int.right)
        {
            endValue =Quaternion.Euler(0,0, -1);
            transform.rotation = endValue;
            if (Move(Vector2Int.right, WIDTH - 2, -1, 0, 1))
            {
                transform.rotation = currentRotation;
                transform.DORotateQuaternion(endValue, 0.3f);
                Solana2048Service.Instance.PushInDirection(true, 0);
                DirectionIndicator.SetDirection(Vector2Int.right);
            }
        }
        if (cachedInput == Vector2Int.down)
        {
            endValue =Quaternion.Euler(-1,0, 0);
            transform.rotation = endValue;
            if (Move(Vector2Int.down, 0, 1, HEIGHT - 2, -1))
            {
                transform.rotation = currentRotation;
                transform.DORotateQuaternion(endValue, 0.3f);
                Solana2048Service.Instance.PushInDirection(true, 1);
                DirectionIndicator.SetDirection(Vector2Int.down);
            }
        }
        if (cachedInput == Vector2Int.left)
        {
            endValue =Quaternion.Euler(0,0, 1);
            transform.rotation = endValue;

            if (Move(Vector2Int.left, 1, 1, 0, 1))
            {
                transform.rotation = currentRotation;
                transform.DORotateQuaternion(endValue, 0.3f);

                Solana2048Service.Instance.PushInDirection(true, 2);
                DirectionIndicator.SetDirection(Vector2Int.left);
            }
        }
        if (cachedInput == Vector2Int.up)
        {
            endValue =Quaternion.Euler(1,0, 0);
            transform.rotation = endValue;

            if (Move(Vector2Int.up, 0, 1, 1, 1))
            {
                transform.rotation = currentRotation;
                transform.DORotateQuaternion(endValue, 0.3f);
                
                Solana2048Service.Instance.PushInDirection(true, 3);
                DirectionIndicator.SetDirection(Vector2Int.up);
            }
        }

        cachedInput = null;
    }

    public Cell GetCell(int x, int y)
    {
        if (x >= 0 && x < WIDTH && y >= 0 && y < HEIGHT) {
            return AllCells[y, x];
        } 
        
        return null;
    }

    public Cell GetAdjacentCell(Cell cell, Vector2Int direction)
    {
        int adjecentX = cell.X + direction.x;
        int adjecentY = cell.Y - direction.y;

        return GetCell(adjecentX, adjecentY);
    }

    private bool Move(Vector2Int direction, int startX, int incrementX, int startY, int incrementY)
    {
        HighestMergedTileThisMove = 0;
        foreach (var tile in tiles) {
            tile.IsLocked = false;
        }

        MergeAudioSource.pitch = 1;
        bool moved = false;

        for (int x = startX; x >= 0 && x < WIDTH; x += incrementX)
        {
            for (int y = startY; y >= 0 && y < HEIGHT; y += incrementY)
            {
                Cell cell = GetCell(x, y);

                if (cell.IsOccupied()) {
                    moved |= MoveTile(cell.Tile, direction);
                }
            }
        }

        if (moved)
        {
            if (SoundToggle.IsSoundEnabled())
            {
                MoveAudioSource.PlayOneShot(Random.Range(0, 2) == 1 ? MoveClip : Move2Clip);
            }

            IsWaiting = true;
            SocketMessageTimeout = DateTime.UtcNow;
        }

        return moved;
    }

    private bool MoveTile(Tile tile, Vector2Int direction)
    {
        Cell newCell = null;
        Cell adjacent = GetAdjacentCell(tile.Cell, direction);

        while (adjacent != null)
        {
            if (adjacent.IsOccupied())
            {
                if (CanMerge(tile, adjacent.Tile))
                {
                    MergeTiles(tile, adjacent.Tile);
                    return true;
                }

                break;
            }

            newCell = adjacent;
            adjacent = GetAdjacentCell(adjacent, direction);
        }

        if (newCell != null)
        {
            tile.MoveTo(newCell);
            return true;
        }

        return false;
    }

    private bool CanMerge(Tile a, Tile b)
    {
        return a.currentConfig == b.currentConfig && !b.IsLocked;
    }

    private void MergeTiles(Tile a, Tile b)
    {
        tiles.Remove(a);
        a.Merge(b.Cell, () =>
        {
            int index = Mathf.Clamp(IndexOf(b.currentConfig) + 1, 0, tileConfigs.Length - 1);
            TileConfig newState = tileConfigs[index];

            if (b != null)
            {
                if (SoundToggle.IsSoundEnabled())
                {
                    //var newPitch = 1 + 0.05f * (index -1);
                    //MergeAudioSource.pitch = MergeAudioSource.pitch < newPitch ? newPitch : MergeAudioSource.pitch;
                    //MergeAudioSource.PlayOneShot(MergeClip);
                    if (HighestMergedTileThisMove == b.currentConfig.Index)
                    {
                        b.PlayMergeSound(MergeClip);   
                    }
                }
                var blimp = Instantiate(BlimpPrefab);
                blimp.SetData(b.currentConfig.Number.ToString());
                blimp.transform.position = b.transform.position;
                StartCoroutine(DestroyAfterSeconds(blimp));
                b.UpdateVisualState();
                //b.PlayMergeSound();
                //Solana2048Service.Instance.CurrentPlayerData.Score += newState.Number;
            }
            else
            {
                Debug.Log("Tile was destroyed during move.");
            }
        });
        int index = Mathf.Clamp(IndexOf(b.currentConfig) + 1, 0, tileConfigs.Length - 1);
        TileConfig newState = tileConfigs[index];
        b.Init(newState, false);
        HighestMergedTileThisMove = HighestMergedTileThisMove < b.currentConfig.Index ? b.currentConfig.Index: HighestMergedTileThisMove;

        // TODO: Animate score
        //gameManager.IncreaseScore(newState.number);
    }

    private IEnumerator DestroyAfterSeconds(TextBlimp3D blimp)
    {
        yield return new WaitForSeconds(2);
        Destroy(blimp.gameObject);
    }

    private int IndexOf(TileConfig state)
    {
        for (int i = 0; i < tileConfigs.Length; i++)
        {
            if (state == tileConfigs[i]) {
                return i;
            }
        }

        return -1;
    }
    
    private void CreateStartingTiles(PlayerData playerData)
    {
        for (int x = 0; x < WIDTH; x++)
        {
            for (int y = 0; y < HEIGHT; y++)
            {
                if (playerData.Board.Data[x][y] != 0)
                {
                    SpawnNewTile(x, y, playerData.Board.Data[x][y]);
                }
            } 
        }
    }

    private void SpawnNewTile(int i, int j, uint number, Color? overrideColor = null)
    {
        if (number == 0)
        {
            return;
        }
        
        var targetCell = AllCells[i, j];
        if (targetCell.Tile != null)
        {
            Debug.LogError("Target cell already full: " + targetCell.Tile.currentConfig.Number);
            return;
        }
        
        if (SoundToggle.IsSoundEnabled())
        {
            SpawnAudioSource.PlayOneShot(SpawnClip);
        }
        
        Tile tileInstance = Instantiate(TilePrefab, transform);
        
        tileInstance.transform.position = targetCell.transform.position;
        TileConfig newConfig = FindTileConfigByNumber(number);
        if (overrideColor != null)
        {
            newConfig.MaterialColor = overrideColor.Value;
            //EditorUtility.SetDirty(newConfig);
        }

        tileInstance.Init(newConfig);
        tileInstance.Spawn(targetCell);
        tiles.Add(tileInstance);
    }

    public void SpawnTestTiles()
    {
        isInitialized = false;
        foreach (Tile tile in tiles) {
            Destroy(tile.gameObject);
        }
        tiles.Clear();
        
        for (int i = 0; i < WIDTH; i++)
        {
            for (int j = 0; j < HEIGHT; j++)
            {
                AllCells[i, j].Tile = null;
            }
        }

        int count = 0;
        uint number = 2;
        foreach (var test in Asset.text.Split(" "))
        {
            ColorUtility.TryParseHtmlString(test, out Color colors);
            Debug.Log("Color: " + test);
            SpawnNewTile(count / 4, count % 4, number, colors);
            count++;
            number = number * 2;
        }
    }

    private TileConfig FindTileConfigByNumber(uint number)
    {
        foreach (var tileConfig in tileConfigs)
        {
            if (tileConfig.Number == number)
            {
                return tileConfig;
            }
        }

        return tileConfigs[tileConfigs.Length - 1];
    }
}
