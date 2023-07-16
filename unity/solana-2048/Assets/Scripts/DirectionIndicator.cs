using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DirectionIndicator : MonoBehaviour
{
    public Image Image;
    
    public Sprite Left;
    public Sprite Down;
    public Sprite Right;
    public Sprite Up;

    public void SetDirection(Vector2Int? direction)
    {
        Image.gameObject.SetActive(direction != null);
        if (direction == Vector2Int.left)
        {
            Image.sprite = Left;
        } else if (direction == Vector2Int.down)
        {
            Image.sprite = Down;
        } else if (direction == Vector2Int.right)
        {
            Image.sprite = Right;
        } else if (direction == Vector2Int.up)
        {
            Image.sprite = Up;
        }
    }
}
