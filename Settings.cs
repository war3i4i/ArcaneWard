using System;
using GUIFramework;
using HarmonyLib;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Valheim.SettingsGui;
using Object = UnityEngine.Object;

namespace kg_ArcaneWard;

    [HarmonyPatch(typeof(FejdStartup), nameof(FejdStartup.Awake))]
    static class Menu_Start_Patch
    {
        private static bool firstInit = true; 
        
        [UsedImplicitly]
        private static void Postfix(FejdStartup __instance)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return; 
            if (!firstInit) return;
            firstInit = false;
            GameObject settingsPrefab = __instance.m_settingsPrefab;
            Transform gameplay = settingsPrefab.transform.Find("Panel/TabButtons/Gameplay");
            if (!gameplay) gameplay = settingsPrefab.transform.Find("Panel/TabButtons/Tabs/Gameplay");
            if (!gameplay) return;
            Transform newButton = Object.Instantiate(gameplay);
            newButton.transform.Find("KeyHint").gameObject.SetActive(false); 
            newButton.SetParent(gameplay.parent, false); 
            newButton.name = "kg_ArcaneWard";
            newButton.SetAsLastSibling();
            Transform textTransform = newButton.transform.Find("Label");
            Transform textTransform_Selected = newButton.transform.Find("Selected/LabelSelected");
            if (!textTransform || !textTransform_Selected) return;
            textTransform.GetComponent<TMP_Text>().text = "$kg_arcaneward".Localize();
            textTransform_Selected.GetComponent<TMP_Text>().text = "$kg_arcaneward".Localize();
            TabHandler tabHandler = settingsPrefab.transform.Find("Panel/TabButtons").GetComponent<TabHandler>();
            Transform page = settingsPrefab.transform.Find("Panel/TabContent");
            GameObject newPage = Object.Instantiate(ArcaneWardUI.Asset.LoadAsset<GameObject>("ArcaneWardSettings"));
            newPage.AddComponent<ArcaneWardSettings>();
            Localization.instance.Localize(newPage.transform);
            newPage.transform.SetParent(page);  
            newPage.name = "kg_ArcaneWard";
            newPage.SetActive(false);
            TabHandler.Tab newTab = new TabHandler.Tab
            { 
                m_default = false,
                m_button = newButton.GetComponent<Button>(),
                m_page = newPage.GetComponent<RectTransform>()
            };
            tabHandler.m_tabs.Add(newTab);
        }
    }

public class ArcaneWardSettings : SettingsBase
{
    public override void FixBackButtonNavigation(Button backButton)
    {
        
    }

    public override void FixOkButtonNavigation(Button okButton)
    {
        
    }

    private GuiToggle _castShadows;
    private GuiToggle _wardSound;
    private GuiToggle _wardFlash;
    private GuiToggle _areaMarker;
    private GuiToggle _useShiftLeftClick;
    private GuiToggle _radiusOnMap;
    private GuiToggle _showIconsOnMap;
    public override void LoadSettings()
    {
        _castShadows = this.transform.Find("List/CastShadows").GetComponent<GuiToggle>();
        _wardSound = this.transform.Find("List/WardSound").GetComponent<GuiToggle>();
        _wardFlash = this.transform.Find("List/Flash").GetComponent<GuiToggle>();
        _areaMarker = this.transform.Find("List/AreaMarker").GetComponent<GuiToggle>();
        _useShiftLeftClick = this.transform.Find("List/ShiftLeftClick").GetComponent<GuiToggle>();
        _radiusOnMap = this.transform.Find("List/RadiusOnMap").GetComponent<GuiToggle>();
        _showIconsOnMap = this.transform.Find("List/ShowIconsOnMap").GetComponent<GuiToggle>();
        _castShadows.isOn = ArcaneWard.CastShadows.Value;
        _wardSound.isOn = ArcaneWard.WardSound.Value;
        _wardFlash.isOn = ArcaneWard.WardFlash.Value;
        _areaMarker.isOn = ArcaneWard.ShowAreaMarker.Value;
        _useShiftLeftClick.isOn = ArcaneWard.UseShiftLeftClick.Value;
        _radiusOnMap.isOn = ArcaneWard.RadiusOnMap.Value;
        _showIconsOnMap.isOn = ArcaneWard.ShowIconsOnMap.Value;
    } 

    public override void SaveSettings() 
    { 
        ArcaneWard.CastShadows.Value = _castShadows.isOn; 
        ArcaneWard.WardSound.Value = _wardSound.isOn;
        ArcaneWard.WardFlash.Value = _wardFlash.isOn;
        ArcaneWard.ShowAreaMarker.Value = _areaMarker.isOn;
        ArcaneWard.UseShiftLeftClick.Value = _useShiftLeftClick.isOn;
        ArcaneWard.RadiusOnMap.Value = _radiusOnMap.isOn;
        ArcaneWard.ShowIconsOnMap.Value = _showIconsOnMap.isOn;
        ArcaneWard._thistype.Config.Save();
        ArcaneWard.ApplyOptions(_castShadows.isOn, _wardSound.isOn);
        Saved();
    }
}