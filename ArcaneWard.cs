using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using fastJSON;
using HarmonyLib;
using JetBrains.Annotations;
using LocalizationManager;
using ServerSync;
using UnityEngine;
using UnityEngine.Rendering;

namespace kg_ArcaneWard
{
    [BepInDependency("org.bepinex.plugins.guilds", BepInDependency.DependencyFlags.SoftDependency)]
    [BepInPlugin(GUID, NAME, VERSION)]
    public class ArcaneWard : BaseUnityPlugin
    {
        private const string GUID = "kg.ArcaneWard"; 
        private const string NAME = "Arcane Ward"; 
        private const string VERSION = "0.6.0";
        
        private static readonly ConfigSync configSync = new ConfigSync(GUID)
            { DisplayName = NAME, CurrentVersion = VERSION, MinimumRequiredVersion = VERSION, IsLocked = true, ModRequired = true};

        public static ArcaneWard _thistype;
        public static ConfigEntry<string> WardRecipe;
        public static ConfigEntry<int> WardDefaultRadius;
        public static ConfigEntry<int> WardMinRadius;
        public static ConfigEntry<int> WardMaxRadius;
        public static ConfigEntry<int> WardMaxDistanceToFuel;
        public static ConfigEntry<string> WardFuelPrefabs;
        public static ConfigEntry<int> WardMaxFuel;
        public static ConfigEntry<bool> WardBlockProjectiles;
        public static ConfigEntry<Protection> DisabledProtection;
        
        public static ConfigEntry<bool> CastShadows;
        public static ConfigEntry<bool> WardSound;
        public static ConfigEntry<bool> WardFlash;
        public static ConfigEntry<bool> ShowAreaMarker;
        public static ConfigEntry<bool> UseShiftLeftClick;
        public static ConfigEntry<bool> RadiusOnMap;
        
        public static GameObject FlashShield;
        public static GameObject FlashShield_Permit;
        public static GameObject FlashShield_Fuel;
        public static GameObject FlashShield_Activate;
        public static GameObject FlashShield_Deactivate;
        
        public static AssetBundle GetAssetBundle(string filename)
        { 
            Assembly execAssembly = Assembly.GetExecutingAssembly();
            string resourceName = execAssembly.GetManifestResourceNames().Single(str => str.EndsWith(filename));
            using Stream stream = execAssembly.GetManifestResourceStream(resourceName)!;
            return AssetBundle.LoadFromStream(stream);
        }

        public static AssetBundle Asset;
        public static GameObject ArcaneWard_Piece;
        public static Sprite ArcaneWard_Icon;
        public static Sprite ArcaneWard_Radius_Icon;
        public static Sprite ArcaneWard_Radius_Icon_Disabled;

        private void Awake()
        {
            JSON.Parameters = new JSONParameters()
            {
                UseExtensions = false,
            };
            
            Localizer.Load();
            _thistype = this;
            Asset = GetAssetBundle("kg_arcaneward");
            ArcaneWard_Piece = Asset.LoadAsset<GameObject>("ArcaneWard");
            ArcaneWard_Piece.GetComponent<ZNetView>().m_distant = true;
            ArcaneWard_Piece.AddComponent<ArcaneWardComponent>();
            ArcaneWard_Icon = ArcaneWard_Piece.GetComponent<Piece>().m_icon;
            
            WardRecipe = config("General", "WardRecipe", "Iron:10:true,Wood:5:true", "The recipe for the Arcane Ward");
            WardRecipe.SettingChanged += (_, _) => ZNetScene_Awake_Patch.ResetRecipe();
            WardDefaultRadius = config("General", "WardDefaultRadius", 30, "The default radius of the Arcane Ward");
            WardMinRadius = config("General", "WardMinRadius", 10, "The minimum radius of the Arcane Ward");
            WardMaxRadius = config("General", "WardMaxRadius", 100, "The maximum radius of the Arcane Ward");
            WardMaxDistanceToFuel = config("General", "WardMaxDistanceToFuel", int.MaxValue, "The maximum distance to fuel the Arcane Ward");
            WardFuelPrefabs = config("General", "WardFuelPrefabs", "Resin,1200,Wood,2400,Coal,3600,Coins,7200", "The prefabs that can be used as fuel for the Arcane Ward");
            WardMaxFuel = config("General", "WardMaxFuel", 604800, "The maximum amount of fuel the Arcane Ward can hold");
            DisabledProtection = config("General", "DisabledProtection", Protection.None, "List of disabled Protection flags");
            WardBlockProjectiles = config("General", "WardBlockProjectiles", true, "Whether the Arcane Ward should block projectiles");
            
            CastShadows = Config.Bind("Visuals", "CastShadows", true, "Whether the Arcane Ward Bubble should cast shadows");
            WardSound = Config.Bind("Visuals", "WardSound", true, "Whether the Arcane Ward should play a sound when activated");
            WardFlash = Config.Bind("Visuals", "WardFlash", true, "Whether the Arcane Ward should flash triggered");
            ShowAreaMarker = Config.Bind("Visuals", "AreaMarker", true, "Whether the Arcane Ward should display an area marker");
            UseShiftLeftClick = Config.Bind("General", "UseShiftLeftClick", false, "Whether the Arcane Ward should use Shift + Left Click to open UI from map or just Left Click");
            RadiusOnMap = Config.Bind("General", "RadiusOnMap", true, "Whether the Arcane Ward should show its radius on the map");
            
            ApplyOptions(CastShadows.Value, WardSound.Value);
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                ArcaneWardUI.Init();
                Guilds.API.RegisterOnGuildJoined(((guild, player) => Cache.RecacheGuildID()));
                Guilds.API.RegisterOnGuildLeft(((guild, player) => Cache.RecacheGuildID()));
            } 
            ServerSide.ServerSideInit();
            new Harmony(GUID).PatchAll();
        }
        public static void ApplyOptions(bool castShadows, bool wardSound)
        {
            ArcaneWard_Piece.transform.Find("Bubble").GetComponent<MeshRenderer>().shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            ArcaneWardComponent._instances.ForEach(x => x._piece.transform.Find("Bubble").GetComponent<MeshRenderer>().shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off);
            ArcaneWard_Piece.transform.Find("VFX/sfx").gameObject.SetActive(wardSound);
            ArcaneWardComponent._instances.ForEach(x => x._piece.transform.Find("VFX/sfx").gameObject.SetActive(wardSound));
        }
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape) && ArcaneWardUI.IsVisible) ArcaneWardUI.Hide();   
        }
        [HarmonyPatch(typeof(ZNetScene),nameof(ZNetScene.Awake))]
        private static class ZNetScene_Awake_Patch
        {
            public static void ResetRecipe()
            {
                if (!ZNetScene.instance) return;
                Piece awp = ArcaneWard_Piece.GetComponent<Piece>();
                List<Piece.Requirement> requirements = [];
                string[] reqs = WardRecipe.Value.Split(',');
                for (var i = 0; i < reqs.Length; ++i)
                {
                    string[] split = reqs[i].Split(':');
                    if (split.Length != 3) continue;
                    if (!int.TryParse(split[1], out int amount)) continue;
                    if (!bool.TryParse(split[2], out bool recover)) continue;
                    requirements.Add(new Piece.Requirement()
                    {
                        m_resItem = ObjectDB.instance.GetItemPrefab(split[0]).GetComponent<ItemDrop>(),
                        m_amount = amount,
                        m_recover = recover
                    });
                }
                awp.m_resources = requirements.ToArray();
                for (var i = 0; i < ArcaneWardComponent._instances.Count; ++i)
                {
                    ArcaneWardComponent._instances[i]._piece.m_resources = requirements.ToArray();
                }
            }
            
            private static bool done;
            private static void Postfix(ZNetScene __instance)
            {
                ResetRecipe();
                if (done) return;
                done = true;
                PrivateArea guardstone = __instance.GetPrefab("guard_stone").GetComponent<PrivateArea>();
                FlashShield_Activate = guardstone.m_activateEffect.m_effectPrefabs[0].m_prefab;
                FlashShield_Deactivate = guardstone.m_deactivateEffect.m_effectPrefabs[0].m_prefab;
                FlashShield = guardstone.m_flashEffect.m_effectPrefabs[0].m_prefab;
                FlashShield_Permit = guardstone.m_addPermittedEffect.m_effectPrefabs[0].m_prefab;
                FlashShield_Fuel = guardstone.m_removedPermittedEffect.m_effectPrefabs[0].m_prefab;
                Piece p = ArcaneWard_Piece.GetComponent<Piece>();
                
                Piece ward = guardstone.GetComponent<Piece>();
                p.m_placeEffect = ward.m_placeEffect;

                var shieldGen = __instance.GetPrefab("charred_shieldgenerator");
                if (!shieldGen) return;
                ArcaneWard_Piece.transform.Find("Bubble").GetComponent<MeshRenderer>().material.shader = shieldGen.transform.Find("ForceField/ForceField").GetComponent<MeshRenderer>().material.shader;
            }
        }
        private static ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description, bool synchronizedSetting = true)
        {
            ConfigEntry<T> configEntry = _thistype.Config.Bind(group, name, value, description);
            SyncedConfigEntry<T> syncedConfigEntry = configSync.AddConfigEntry(configEntry);
            syncedConfigEntry.SynchronizedConfig = synchronizedSetting;
            return configEntry;
        }
        private static ConfigEntry<T> config<T>(string group, string name, T value, string description, bool synchronizedSetting = true) => config(group, name, value, new ConfigDescription(description), synchronizedSetting);
    }
}