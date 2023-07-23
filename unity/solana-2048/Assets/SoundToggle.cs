using UnityEngine;
using UnityEngine.UI;

public class SoundToggle : MonoBehaviour
{
    public Button OnButton;
    public Button OffButton;

    private const string SoundOffKey = "SoundOff";

    public static bool IsSoundEnabled()
    {
        return !PlayerPrefs.HasKey(SoundOffKey);
    }
    
    private void Awake()
    {
        OnButton.onClick.AddListener(OnOnButtonClicked);
        OffButton.onClick.AddListener(OnOffButtonClicked);
        
        UpdateContent();
    }

    private void OnOffButtonClicked()
    {
        PlayerPrefs.SetInt(SoundOffKey, 1);
        UpdateContent();
    }

    private void OnOnButtonClicked()
    {
        PlayerPrefs.DeleteKey(SoundOffKey);
        UpdateContent();
    }

    private void UpdateContent()
    {
        OnButton.gameObject.SetActive(PlayerPrefs.HasKey(SoundOffKey));
        OffButton.gameObject.SetActive(!PlayerPrefs.HasKey(SoundOffKey));
    }
}
