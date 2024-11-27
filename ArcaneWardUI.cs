using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace kg_ArcaneWard;

public static class ArcaneWardUI
{
    private static GameObject UI;
    private static ZDO _currentWard;
    
    //common
    private static readonly Button[] TabButtons = new Button[3];
    private static readonly Transform[] Tabs = new Transform[3];
    private static Button SaveButton;
    //overview tab
    private static TMP_InputField Name;
    private static Button Enabled;
    private static Button Bubble;
    private static Button Notify;
    private static Slider Radius;
    private static TMP_Text RadiusText;
    private static Slider BubbleFraction;
    private static TMP_Text BubbleFractionText;
    private static TMP_Text Fuel;
    private static GameObject FuelEntry;
    //protection tab
    private static GameObject ProtectionEntry;
    //permissions tab
    private static GameObject PermittedEntry;
    private static GameObject AddPermittedEntry;

    private static string EnabledLocalized;
    private static string DisabledLocalized;
    
    private static Dictionary<Protection, Sprite> ProtectionIcons;

    public static AssetBundle Asset;
    public static void Init()
    {
        Asset = ArcaneWard.GetAssetBundle("kg_arcanewardui");
        ArcaneWard.ArcaneWard_Radius_Icon = Asset.LoadAsset<Sprite>("ArcaneWardCircle_Img");
        ArcaneWard.ArcaneWard_Radius_Icon_Disabled = Asset.LoadAsset<Sprite>("ArcaneWardCircle_Img_Disabled");
        UI = Object.Instantiate(Asset.LoadAsset<GameObject>("ArcaneWardUI"));
        Object.DontDestroyOnLoad(UI);
        UI.SetActive(false);
        //main
        TabButtons[0] = UI.transform.Find("Canvas/UI/TopButtons/Overview").GetComponent<Button>(); 
        TabButtons[1] = UI.transform.Find("Canvas/UI/TopButtons/Protection").GetComponent<Button>();
        TabButtons[2] = UI.transform.Find("Canvas/UI/TopButtons/Permissions").GetComponent<Button>();
        Tabs[0] = UI.transform.Find("Canvas/UI/Tabs/Overview");
        Tabs[1] = UI.transform.Find("Canvas/UI/Tabs/Protection");
        Tabs[2] = UI.transform.Find("Canvas/UI/Tabs/Permissions");
        SaveButton = UI.transform.Find("Canvas/UI/Save").GetComponent<Button>();
        for(int i = 0; i < 3; ++i)
        {
            int index = i;
            TabButtons[i].onClick.AddListener(() =>
            {
                for(int j = 0; j < 3; j++)
                {
                    Tabs[j].gameObject.SetActive(j == index);
                    TabButtons[j].interactable = j != index;
                    TabButtons[j].transform.Find("text").GetComponent<TMP_Text>().color = j == index ? new Color(1f, 0.59f, 0.12f) : Color.white;
                }
            });
        }
        SaveButton.onClick.AddListener(() =>
        {
            Save();
            UI.SetActive(false);
        });
        //overview
        Name = Tabs[0].Find("Name").GetComponent<TMP_InputField>();
        Enabled = Tabs[0].Find("Options/Enabled/Enabled").GetComponent<Button>();
        Bubble = Tabs[0].Find("Options/Bubble/Enabled").GetComponent<Button>();
        Notify = Tabs[0].Find("Options/Notify/Enabled").GetComponent<Button>();
        Radius = Tabs[0].Find("Options/Radius/Slider").GetComponent<Slider>();
        RadiusText = Tabs[0].Find("Options/Radius/SliderText").GetComponent<TMP_Text>();
        Radius.onValueChanged.AddListener(value => RadiusText.text = value.ToString(CultureInfo.InvariantCulture));
        BubbleFraction = Tabs[0].Find("Options/BubbleFraction/Slider").GetComponent<Slider>();
        BubbleFraction.onValueChanged.AddListener(value => BubbleFractionText.text = value.ToString(CultureInfo.InvariantCulture));
        BubbleFractionText = Tabs[0].Find("Options/BubbleFraction/SliderText").GetComponent<TMP_Text>();
        Fuel = Tabs[0].Find("Fuel/text").GetComponent<TMP_Text>();
        FuelEntry = Tabs[0].Find("Fuel/AddFuel/Scroll View/Viewport/Content/FuelEntry").gameObject;
        Enabled.onClick.AddListener(() =>
        {
            bool enabled = Enabled.gameObject.name == "+";
            Enabled.gameObject.name = enabled ? "-" : "+";
            Enabled.transform.Find("text").GetComponent<TMP_Text>().text = enabled ? DisabledLocalized : EnabledLocalized;
            Enabled.transform.Find("text").GetComponent<TMP_Text>().color = enabled ? Color.red : Color.green;
        });
        Bubble.onClick.AddListener(() =>
        {
            bool enabled = Bubble.gameObject.name == "+";
            Bubble.gameObject.name = enabled ? "-" : "+";
            Bubble.transform.Find("text").GetComponent<TMP_Text>().text = enabled ? DisabledLocalized : EnabledLocalized;
            Bubble.transform.Find("text").GetComponent<TMP_Text>().color = enabled ? Color.red : Color.green;
        });
        Notify.onClick.AddListener(() =>
        {
            bool enabled = Notify.gameObject.name == "+";
            Notify.gameObject.name = enabled ? "-" : "+";
            Notify.transform.Find("text").GetComponent<TMP_Text>().text = enabled ? DisabledLocalized : EnabledLocalized;
            Notify.transform.Find("text").GetComponent<TMP_Text>().color = enabled ? Color.red : Color.green;
        });
        //protection
        ProtectionEntry = Tabs[1].Find("Scroll View/Viewport/Content/ProtectionEntry").gameObject;
        //permissions
        PermittedEntry = Tabs[2].Find("Permitted/Viewport/Content/PermittedEntry").gameObject;
        AddPermittedEntry = Tabs[2].Find("AddPermitted/Viewport/Content/AddPermittedEntry").gameObject;

        EnabledLocalized = "$kg_arcaneward_enabled".Localize();
        DisabledLocalized = "$kg_arcaneward_disabled".Localize();
        Localization.instance.Localize(UI.transform);
    }

    private static void Save()
    {
        Hide();
        if (_currentWard == null || !_currentWard.IsValid()) return;
        if (_currentWard.HasOwner())
        {
            ZPackage pkg = new();
            pkg.Write(Name.text ?? "");
            pkg.Write(Enabled.gameObject.name == "+");
            pkg.Write(Bubble.gameObject.name == "+");
            pkg.Write(Notify.gameObject.name == "+");
            int radius = Mathf.Clamp((int)Radius.value, ArcaneWard.WardMinRadius.Value, ArcaneWard.WardMaxRadius.Value);
            pkg.Write(radius);
            int fraction = Mathf.Clamp((int)BubbleFraction.value, 0, 20);
            pkg.Write(fraction); 
            Protection val = Protection.None;
            for(int i = 1; i < ProtectionEntry.transform.parent.childCount; ++i)
            {
                Transform entry = ProtectionEntry.transform.parent.GetChild(i);
                long value = long.Parse(entry.gameObject.name);
                val |= (Protection)value;
            }
            pkg.Write((long)val);
            pkg.Write(_tempPermittedList.Count);
            foreach (var user in _tempPermittedList)
            {
                pkg.Write(user.Key);
                pkg.Write(user.Value);
            }
            ZRoutedRpc.instance.InvokeRoutedRPC(_currentWard.GetOwner(), _currentWard.m_uid, "RPC_UpdateData", pkg);
        }
        else
        { 
            _currentWard.Set(ArcaneWardComponent._cache_Key_Name, Name.text ?? ""); 
            _currentWard.Set(ArcaneWardComponent._cache_Key_Enabled, Enabled.gameObject.name == "+");
            _currentWard.Set(ArcaneWardComponent._cache_Key_Bubble, Bubble.gameObject.name == "+");
            _currentWard.Set(ArcaneWardComponent._cache_Key_Notify, Notify.gameObject.name == "+");
            int radius = Mathf.Clamp((int)Radius.value, ArcaneWard.WardMinRadius.Value, ArcaneWard.WardMaxRadius.Value);
            _currentWard.Set(ArcaneWardComponent._cache_Key_Radius, radius);
            int fraction = Mathf.Clamp((int)BubbleFraction.value, 0, 20);
            _currentWard.Set(ArcaneWardComponent._cache_Key_BubbleFraction, fraction);
            Protection val = Protection.None;
            for(int i = 1; i < ProtectionEntry.transform.parent.childCount; ++i)
            {
                Transform entry = ProtectionEntry.transform.parent.GetChild(i);
                long value = long.Parse(entry.gameObject.name);
                val |= (Protection)value;
            } 
            _currentWard.Set(ArcaneWardComponent._cache_Key_Protection, (long)val);
            _currentWard.SetPermittedPlayers(_tempPermittedList);
            _currentWard.Set(ArcaneWardComponent._cache_Key_LastUpdateTime, (int)EnvMan.instance.m_totalSeconds);
        }
    }
    public static void Hide()
    {
        if (_fuelTextCoroutine != null) ArcaneWard._thistype.StopCoroutine(_fuelTextCoroutine);
        UI.SetActive(false);
    }
    private static void UpdateFuel()
    {
        if (_currentWard == null || !_currentWard.IsValid() || !Player.m_localPlayer) return;
        FuelEntry.transform.parent.RemoveAllChildrenExceptFirst();
    
        Vector3 wardPos = _currentWard.GetPosition();
        float distance = Vector3.Distance(Player.m_localPlayer.transform.position, wardPos);
        if (distance > ArcaneWard.WardMaxDistanceToFuel.Value) return;
        
        string[] split = ArcaneWard.WardFuelPrefabs.Value.Split(',');
        for(int i = 0; i < split.Length; i += 2)
        {
            GameObject entry = Object.Instantiate(FuelEntry, FuelEntry.transform.parent);
            entry.SetActive(true); 
            var item = ZNetScene.instance.GetPrefab(split[i]).GetComponent<ItemDrop>().m_itemData;
            entry.transform.Find("Icon").GetComponent<Image>().sprite = item.GetIcon();
            int addSeconds = int.Parse(split[i + 1]);
            int inventoryAmount = Player.m_localPlayer.GetInventory().CountItems(item.m_shared.m_name);
            entry.transform.Find("text").GetComponent<TMP_Text>().text = Localization.instance.Localize($"{item.m_shared.m_name} ({addSeconds.ToTime()}) [{inventoryAmount}]");
            entry.transform.Find("text").GetComponent<TMP_Text>().color = inventoryAmount >= 1 ? new Color(0.57f, 1f, 0.51f) : new Color(1f, 0.25f, 0.39f);
            Button button = entry.transform.Find("Add").GetComponent<Button>();
            button.transform.Find("text").GetComponent<TMP_Text>().color = inventoryAmount >= 1 ? new Color(0.57f, 1f, 0.51f) : new Color(1f, 0.25f, 0.39f);
            button.interactable = inventoryAmount >= 1;
            button.onClick.AddListener(() =>
            {
                int amount = Player.m_localPlayer.GetInventory().CountItems(item.m_shared.m_name);
                if (amount < 1) return;
                Player.m_localPlayer.GetInventory().RemoveItem(item.m_shared.m_name, 1);
                if (_currentWard.HasOwner()) ZRoutedRpc.instance.InvokeRoutedRPC(_currentWard.GetOwner(), _currentWard.m_uid, "RPC_AddFuel", [addSeconds]);
                else _currentWard.Set(ArcaneWardComponent._cache_Key_Fuel, _currentWard.GetFloat(ArcaneWardComponent._cache_Key_Fuel) + addSeconds);
                UpdateFuel();
            });
        }
    }
    private static void UpdateProtection()
    {
        if (_currentWard == null || !_currentWard.IsValid()) return;
        ProtectionEntry.transform.parent.RemoveAllChildrenExceptFirst();
        Protection[] values = ArcaneWardComponent.ProtectionValues;
        Protection wardProtection = (Protection)_currentWard.GetLong(ArcaneWardComponent._cache_Key_Protection, ArcaneWardComponent.FullProtection);
        foreach (var value in values)
        {
            GameObject entry = Object.Instantiate(ProtectionEntry, ProtectionEntry.transform.parent);
            entry.SetActive(true);
            entry.transform.Find("text").GetComponent<TMP_Text>().text = $"$kg_arcaneward_protection_{value.ToString().ToLower()}".Localize();
            entry.transform.Find("Icon").GetComponent<Image>().sprite = ProtectionIcons.TryGetValue(value, out Sprite sprite) ? sprite : null;
            bool hasFlag = wardProtection.HasFlagFast(value); 
            bool disabled = ArcaneWard.DisabledProtection.Value.HasFlagFast(value);
            if (disabled) 
            {
                entry.transform.Find("text").GetComponent<TMP_Text>().color = new Color(1f, 0.25f, 0.39f);
                entry.transform.Find("text").GetComponent<TMP_Text>().text += $" ({DisabledLocalized})";
            }
            entry.gameObject.name = hasFlag ? ((long)value).ToString() : "0";
            Button button = entry.transform.Find("Enabled").GetComponent<Button>();
            button.interactable = !disabled;
            button.transform.Find("text").GetComponent<TMP_Text>().text = hasFlag ? EnabledLocalized : DisabledLocalized;
            button.transform.Find("text").GetComponent<TMP_Text>().color = hasFlag ? new Color(0.57f, 1f, 0.51f) : new Color(1f, 0.25f, 0.39f);
            button.onClick.AddListener(() =>
            {  
                bool enabled = entry.gameObject.name == "0";
                entry.gameObject.name = enabled ? ((long)value).ToString() : "0";
                button.transform.Find("text").GetComponent<TMP_Text>().text = enabled ? EnabledLocalized : DisabledLocalized;
                button.transform.Find("text").GetComponent<TMP_Text>().color = enabled ? new Color(0.57f, 1f, 0.51f) : new Color(1f, 0.25f, 0.39f);
            });
        }
    }
    private static void UpdatePermittedList()
    {
        if (_currentWard == null || !_currentWard.IsValid()) return;
        long creator = _currentWard.GetLong(ZDOVars.s_creator);
        PermittedEntry.transform.parent.RemoveAllChildrenExceptFirst();
        foreach (var user in _tempPermittedList)
        {
            GameObject entry = Object.Instantiate(PermittedEntry, PermittedEntry.transform.parent);
            entry.SetActive(true);
            entry.name = user.Key.ToString();
            entry.transform.Find("text").GetComponent<TMP_Text>().text = user.Value.Localize();
            Button button = entry.transform.Find("Enabled").GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                _tempPermittedList.Remove(user.Key);
                UpdatePermittedList();
                UpdateAddPermittedList();
            });
            if (user.Key == creator) button.interactable = false;
        }
    }
    private static void UpdateAddPermittedList()
    {
        if (_currentWard == null || !_currentWard.IsValid()) return;
        AddPermittedEntry.transform.parent.RemoveAllChildrenExceptFirst();
        IEnumerable<ZNet.PlayerInfo> list = ZNet.instance.GetPlayerList().Where(x => ZDOMan.instance.GetZDO(x.m_characterID) != null && !_tempPermittedList.ContainsKey(ZDOMan.instance.GetZDO(x.m_characterID).GetLong(ZDOVars.s_playerID)));
        foreach (var user in list)
        {
            long id = ZDOMan.instance.GetZDO(user.m_characterID).GetLong(ZDOVars.s_playerID);
            string name = user.m_name;
            if (_tempPermittedList.ContainsKey(id)) continue;
            GameObject entry = Object.Instantiate(AddPermittedEntry, AddPermittedEntry.transform.parent);
            entry.SetActive(true);
            entry.name = id.ToString();
            entry.transform.Find("text").GetComponent<TMP_Text>().text = name.Localize();
            Button button = entry.transform.Find("Enabled").GetComponent<Button>();
            button.onClick.AddListener(() =>
            {
                _tempPermittedList.Add(id, name);
                UpdatePermittedList(); 
                UpdateAddPermittedList();
            });
        }
    }
    private static Dictionary<long, string> _tempPermittedList = [];
    public static void Show(ZDO ward)
    {
        _currentWard = ward;
        if (!_currentWard.IsValid() || !Player.m_localPlayer) return;
        Name.text = ward.GetString(ArcaneWardComponent._cache_Key_Name, "Arcane Ward");
        bool isActivated = ward.GetBool(ArcaneWardComponent._cache_Key_Enabled);
        Enabled.transform.Find("text").GetComponent<TMP_Text>().text = isActivated ? EnabledLocalized : DisabledLocalized;
        Enabled.transform.Find("text").GetComponent<TMP_Text>().color = isActivated ? Color.green : Color.red;
        Enabled.gameObject.name = isActivated ? "+" : "-";
        bool isBubbleEnabled = ward.GetBool(ArcaneWardComponent._cache_Key_Bubble, true);
        Bubble.transform.Find("text").GetComponent<TMP_Text>().text = isBubbleEnabled ? EnabledLocalized : DisabledLocalized;
        Bubble.transform.Find("text").GetComponent<TMP_Text>().color = isBubbleEnabled ? Color.green : Color.red;
        Bubble.gameObject.name = isBubbleEnabled ? "+" : "-";
        bool isNotifyEnabled = ward.GetBool(ArcaneWardComponent._cache_Key_Notify);
        Notify.transform.Find("text").GetComponent<TMP_Text>().text = isNotifyEnabled ? EnabledLocalized : DisabledLocalized;
        Notify.transform.Find("text").GetComponent<TMP_Text>().color = isNotifyEnabled ? Color.green : Color.red;
        Notify.gameObject.name = isNotifyEnabled ? "+" : "-";
        Radius.minValue = ArcaneWard.WardMinRadius.Value;
        Radius.maxValue = ArcaneWard.WardMaxRadius.Value;
        Radius.value = ward.GetInt(ArcaneWardComponent._cache_Key_Radius, ArcaneWard.WardDefaultRadius.Value);
        RadiusText.text = Radius.value.ToString(CultureInfo.InvariantCulture);
        BubbleFraction.minValue = 0;
        BubbleFraction.maxValue = 20;
        BubbleFraction.value = ward.GetInt(ArcaneWardComponent._cache_Key_BubbleFraction, 1); 
        BubbleFractionText.text = BubbleFraction.value.ToString(CultureInfo.InvariantCulture);
        _fuelTextCoroutine = ArcaneWard._thistype.StartCoroutine(UpdateFuelText());
        UpdateFuel();
        UpdateProtection();
        _tempPermittedList = _currentWard.GetPermittedPlayers();
        UpdatePermittedList();
        UpdateAddPermittedList();
        UI.SetActive(true);  
        TabButtons[0].onClick.Invoke();

        var me = Game.instance.m_playerProfile.m_playerID;
        var owner = ward.GetLong(ZDOVars.s_creator);
        TabButtons[2].gameObject.SetActive(me == owner);
    }
    private static Coroutine _fuelTextCoroutine;
    private static IEnumerator UpdateFuelText()
    {
        while (true)
        {
            if (_currentWard == null || !_currentWard.IsValid()) continue;
            if (_currentWard.HasOwner()) 
            {
                float fuel = _currentWard.GetFloat(ArcaneWardComponent._cache_Key_Fuel);
                Fuel.text = $"$kg_arcaneward_fuel: <color={(fuel > 0 ? "green" : "red")}>{((int)fuel).ToTime()}</color> / <color=yellow>{ArcaneWard.WardMaxFuel.Value.ToTimeNoS()}</color>".Localize();
            }
            else
            { 
                if (_currentWard.GetBool(ArcaneWardComponent._cache_Key_Enabled))
                {
                    int currentTime = (int)EnvMan.instance.m_totalSeconds; 
                    int deltaTime = currentTime - _currentWard.GetInt(ArcaneWardComponent._cache_Key_LastUpdateTime);
                    float fuel = Mathf.Clamp(_currentWard.GetFloat(ArcaneWardComponent._cache_Key_Fuel) - deltaTime, 0f, ArcaneWard.WardMaxFuel.Value);
                    _currentWard.Set(ArcaneWardComponent._cache_Key_Fuel, fuel);
                    _currentWard.Set(ArcaneWardComponent._cache_Key_LastUpdateTime, currentTime);
                    if (fuel <= 0)
                    { 
                        _currentWard.Set(ArcaneWardComponent._cache_Key_Enabled, false);
                        Enabled.transform.Find("text").GetComponent<TMP_Text>().text = DisabledLocalized;
                        Enabled.transform.Find("text").GetComponent<TMP_Text>().color = Color.red;
                        Enabled.gameObject.name = "-";
                    }
                    Fuel.text = $"$kg_arcaneward_fuel: <color={(fuel > 0 ? "green" : "red")}>{((int)fuel).ToTime()}</color> / <color=yellow>{ArcaneWard.WardMaxFuel.Value.ToTimeNoS()}</color>".Localize();
                }
                else
                {
                    float fuel = _currentWard.GetFloat(ArcaneWardComponent._cache_Key_Fuel);
                    Fuel.text = $"$kg_arcaneward_fuel: <color={(fuel > 0 ? "green" : "red")}>{((int)fuel).ToTime()}</color> / <color=yellow>{ArcaneWard.WardMaxFuel.Value.ToTimeNoS()}</color>".Localize();
                }
            }
            yield return new WaitForSeconds(1f);
            if (!IsVisible) yield break;
        }
    }
    public static bool IsVisible => UI && UI.activeSelf;
    [HarmonyPatch(typeof(TextInput),nameof(TextInput.IsVisible))]
    private static class TextInput_IsVisible_Patch
    {
        private static void Postfix(ref bool __result) => __result |= IsVisible;
    }
    [HarmonyPatch(typeof(StoreGui),nameof(StoreGui.IsVisible))]
    private static class StoreGui_IsVisible_Patch
    {
        private static void Postfix(ref bool __result) => __result |= IsVisible;
    }
    [HarmonyPatch(typeof(ZNetScene),nameof(ZNetScene.Awake))]
    private static class ZNetScene_Awake_Patch
    {
        private static void Postfix()
        {
            if (ProtectionIcons != null) return;
            ProtectionIcons = [];
            foreach (var field in typeof(Protection).GetFields())
            {
                if (field.GetCustomAttributes(typeof(Extensions.ProtectionIconAttribute), false).FirstOrDefault() is not Extensions.ProtectionIconAttribute attribute) continue;
                ProtectionIcons[(Protection)field.GetValue(null)] = Extensions.TryFindIcon(attribute.Name);
            }
        }
    }
}