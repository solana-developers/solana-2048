using System;
using System.Collections.Generic;
using DefaultNamespace;
using Frictionless;
using Solana2048.Accounts;
using UnityEngine;

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

    public bool IsWaiting;
    public DateTime? SocketMessageTimeout = null;
    
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

    private void OnGameReset()
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
    }

    private void OnPlayerDataChange(PlayerData playerData)
    {
        foreach (var tile in tiles) {
            tile.IsLocked = false;
        }
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

        if (anyTileOutOfSync)
        {
            RefreshFromPlayerdata(playerData);
            return;
        }
        
        IsWaiting = false;
        SocketMessageTimeout = null;
        GameOverText.gameObject.SetActive(playerData.GameOver);
        if (playerData.GameOver)
        {
            Debug.Log("Game over!!");
        }
    }

    private void RefreshFromPlayerdata(PlayerData playerData)
    {
        OnGameReset();
        CreateStartingTiles(playerData);
        isInitialized = true;
        IsWaiting = false;
    }

    private async void Update()
    {
        if (SocketMessageTimeout != null && SocketMessageTimeout + TimeSpan.FromSeconds(5) < DateTime.Now)
        {
            RefreshFromPlayerdata(Solana2048Service.Instance.CurrentPlayerData);
            cachedInput = null;
            SocketMessageTimeout = null;
            await ServiceFactory.Resolve<Solana2048Service>().SubscribeToPlayerDataUpdates();
            return;
        }

        if (Solana2048Service.Instance.CurrentPlayerData == null || Solana2048Service.Instance.CurrentPlayerData.GameOver)
        {
            cachedInput = null;
            return;
        }
        
        if (Solana2048Service.Instance == null || IsWaiting)
        {
            return;
        }
        if (Input.GetKeyUp(KeyCode.RightArrow))
        {
            cachedInput = Vector2Int.right;
        }
        if (Input.GetKeyUp(KeyCode.DownArrow))
        {
            cachedInput = Vector2Int.down;
        }
        if (Input.GetKeyUp(KeyCode.LeftArrow))
        {
            cachedInput = Vector2Int.left;
        }
        if (Input.GetKeyUp(KeyCode.UpArrow)) 
        {
            cachedInput = Vector2Int.up;
        }
        if (cachedInput == Vector2Int.right)
        {
            Move(Vector2Int.right, WIDTH - 2, -1, 0, 1);
            Solana2048Service.Instance.PushInDirection(true, 0);
        }
        if (cachedInput == Vector2Int.down)
        {
            Move(Vector2Int.down, 0, 1, HEIGHT - 2, -1);
            Solana2048Service.Instance.PushInDirection(true, 1);
        }
        if (cachedInput == Vector2Int.left)
        {
            Move(Vector2Int.left, 1, 1, 0, 1);
            Solana2048Service.Instance.PushInDirection(true, 2);
        }
        if (cachedInput == Vector2Int.up)
        {
            Move(Vector2Int.up, 0, 1, 1, 1);
            Solana2048Service.Instance.PushInDirection(true, 3);
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

    private void Move(Vector2Int direction, int startX, int incrementX, int startY, int incrementY)
    {
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
            IsWaiting = true;
            SocketMessageTimeout = DateTime.Now;
        }
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

            b.Init(newState);
        });

        // TODO: Animate score
        //gameManager.IncreaseScore(newState.number);
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

    private void SpawnNewTile(int i, int j, uint number)
    {
        if (number == 0)
        {
            return;
        }
        Tile tileInstance = Instantiate(TilePrefab, transform);
        var targetCell = AllCells[i, j];
        tileInstance.transform.position = targetCell.transform.position;
        TileConfig newConfig = FindTileConfigByNumber(number);

        if (targetCell.Tile != null)
        {
            Debug.LogError("Target cell already full: " + targetCell.Tile.currentConfig.Number);
        }
        tileInstance.Init(newConfig);
        tileInstance.Spawn(targetCell);
        tiles.Add(tileInstance);
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
