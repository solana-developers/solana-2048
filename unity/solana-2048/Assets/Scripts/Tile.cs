using System;
using DefaultNamespace;
using DG.Tweening;
using SolPlay.FlappyGame.Runtime.Scripts;
using TMPro;
using UnityEngine;

public class Tile : MonoBehaviour
{
    public TextMeshPro NumberText;
    public bool IsLocked;
    public MeshRenderer MeshRenderer;
    public TileConfig currentConfig;
    
    public Cell Cell;

    private void Awake()
    {
        if (MeshRenderer == null)
        {
            MeshRenderer = GetComponentInChildren<MeshRenderer>();   
        }
    }
    
    public void Init(TileConfig config)
    {
        currentConfig = config;
        NumberText.text = config.Number.ToString();
        gameObject.SetActive(config.Number > 0);
        MeshRenderer.material = config.Material;
        MeshRenderer.material.color = config.MaterialColor;
    }

    public void Spawn(Cell cell)
    {
        if (Cell != null) {
            Cell.Tile = null;
        }

        Cell = cell;
        Cell.Tile = this;

        transform.position = cell.transform.position;
    }

    public void MoveTo(Cell cell)
    {
        if (Cell != null) {
            Cell.Tile = null;
        }

        Cell = cell;
        Cell.Tile = this;

        transform.DOMove(cell.transform.position, 0.5f).OnComplete(() =>
        {

        });
    }

    public void Merge(Cell cell, Action onMoveDone)
    {
        if (Cell != null) {
            Cell.Tile = null;
        }

        Cell = null;
        cell.Tile.IsLocked = true;

        transform.DOMove(cell.transform.position, 0.5f).OnComplete(() =>
        {
            Destroy(gameObject);
            onMoveDone.Invoke();
            CameraShake.Shake(0.2f, cell.Tile.currentConfig.ShakeStrength);
            if (cell.Tile.currentConfig.MergeFx != null)
            {
                var instance = Instantiate(cell.Tile.currentConfig.MergeFx, cell.transform);
                cell.Tile.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0.1f), 0.3f);
                instance.AddComponent<DestroyDelayed>();
            }
        });    
    }


}
