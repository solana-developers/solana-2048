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
    public AudioClip MergeClip;
    public AudioSource MergeAudioSource;

    public Cell Cell;
    private Vector3 originalScale;
    
    private void Awake()
    {
        if (MeshRenderer == null)
        {
            MeshRenderer = GetComponentInChildren<MeshRenderer>();   
        }

        var cachedTransform = transform;
        originalScale = cachedTransform.localScale;
        cachedTransform.localScale = Vector3.zero;
        transform.DOScale(originalScale, 0.15f);
    }
    
    public void Init(TileConfig config, bool updateVisuals = true)
    {
        currentConfig = config;
        gameObject.SetActive(config.Number > 0);

        if (updateVisuals)
        {
            UpdateVisualState();
        }
    }

    public void UpdateVisualState()
    {
        NumberText.text = currentConfig.Number.ToString();
        gameObject.SetActive(currentConfig.Number > 0);
        MeshRenderer.material = currentConfig.Material;
        MeshRenderer.material.color = currentConfig.MaterialColor;
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

        transform.DOMove(cell.transform.position, 0.45f).OnComplete(() =>
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

        NumberText.transform.DOScale(Vector3.zero, 0.4f);
        transform.DOMove(cell.transform.position, 0.45f).OnComplete(() =>
        {
            Destroy(gameObject);
            onMoveDone.Invoke();
            CameraShake.Shake(0.2f, cell.Tile.currentConfig.ShakeStrength);
            if (cell.Tile.currentConfig.MergeFx != null)
            {
                var instance = Instantiate(cell.Tile.currentConfig.MergeFx, cell.transform);
                cell.Tile.transform.localScale = originalScale;
                cell.Tile.transform.DOPunchScale(new Vector3(0.1f, 0.1f, 0.1f), 0.3f);
                instance.AddComponent<DestroyDelayed>();
            }
        });    
    }
    
    public void PlayMergeSound(AudioClip mergeClip = null)
    {
        MergeAudioSource.pitch = 1 + 0.06f * (currentConfig.Index);
        if (mergeClip != null)
        {
            MergeAudioSource.PlayOneShot(mergeClip);  
        }
        else
        {
            MergeAudioSource.PlayOneShot(MergeClip);
        }
    }
}
