using GUIFramework;
using HarmonyLib;
using JetBrains.Annotations;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Valheim.SettingsGui;

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
    public override void LoadSettings()
    {
        _castShadows = this.transform.Find("List/CastShadows").GetComponent<GuiToggle>();
        _wardSound = this.transform.Find("List/WardSound").GetComponent<GuiToggle>();
        _wardFlash = this.transform.Find("List/EnableFlash").GetComponent<GuiToggle>();
        _castShadows.isOn = ArcaneWard.CastShadows.Value;
        _wardSound.isOn = ArcaneWard.WardSound.Value;
        _wardFlash.isOn = ArcaneWard.WardFlash.Value;
    }

    public override void SaveSettings()
    {
        ArcaneWard.CastShadows.Value = _castShadows.isOn;
        ArcaneWard.WardSound.Value = _wardSound.isOn;
        ArcaneWard.WardFlash.Value = _wardFlash.isOn;
        ArcaneWard._thistype.Config.Save();
        ArcaneWard.ApplyOptions(_castShadows.isOn, _wardSound.isOn);
        Saved();
    }
}