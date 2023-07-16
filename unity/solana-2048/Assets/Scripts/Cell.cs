using TMPro;
using UnityEngine;

public class Cell : MonoBehaviour
{
    public int X { private set; get; }
    public int Y { private set; get; }
    public Tile Tile;

    public void Init(int x, int y, Tile tile)
    {
        X = x;
        Y = y;
        Tile = tile;
    }

    public bool IsEmpty()
    {
        return Tile == null;
    }
    
    public bool IsOccupied()
    {
        return Tile != null;
    }
}
