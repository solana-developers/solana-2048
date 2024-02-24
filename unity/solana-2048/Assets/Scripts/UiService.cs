using System;
using System.Collections;
using System.Collections.Generic;
using Frictionless;
using SolPlay.Scripts.Ui;
using UnityEngine;

namespace SolPlay.Scripts.Services
{
    public class UiService : MonoBehaviour, IMultiSceneSingleton
    {
        [Serializable]
        public class UiRegistration
        {
            public BasePopup PopupPrefab;
            public ScreenType ScreenType;
        }
        
        public enum ScreenType
        {
            TransferNftPopup = 0,
            NftListPopup = 1,
            HighscorePopup = 2,
            SessionPopup = 3,
        }

        public class UiData
        {
            
        }
        
        public List<UiRegistration> UiRegistrations = new List<UiRegistration>();
        
        private readonly Dictionary<ScreenType, BasePopup> instantiatedPopups = new Dictionary<ScreenType, BasePopup>();
        public static int OpenPopups = 0;
            
        public void Awake()
        {
            ServiceFactory.RegisterSingleton(this);
        }

        public bool IsAnyPopupOpen()
        {
            return OpenPopups > 0;
        }

        public void OpenPopup(ScreenType screenType, UiData uiData)
        {
            if (instantiatedPopups.TryGetValue(screenType, out BasePopup basePopup))
            {
                UiService.OpenPopups++;
                basePopup.Open(uiData);
                return;
            }
            
            foreach (var uiRegistration in UiRegistrations)
            {
                if (uiRegistration.ScreenType == screenType)
                {
                    BasePopup newPopup = Instantiate(uiRegistration.PopupPrefab);
                    instantiatedPopups.Add(screenType, newPopup);
                    UiService.OpenPopups++;
                    newPopup.Open(uiData);
                    return;
                }
            }
            
            Debug.LogWarning("There was no screen registration for " + screenType);
        }

        public IEnumerator HandleNewSceneLoaded()
        {
            instantiatedPopups.Clear();
            yield return null;
        }
    }
}