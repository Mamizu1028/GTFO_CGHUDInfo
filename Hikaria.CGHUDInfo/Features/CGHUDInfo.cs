using Gear;
using Hikaria.CGHUDInfo.Utils;
using Localization;
using Player;
using System.Text;
using TheArchive.Core.Attributes;
using TheArchive.Core.Attributes.Feature.Settings;
using TheArchive.Core.FeaturesAPI;
using TheArchive.Core.FeaturesAPI.Settings;
using TheArchive.Core.Localization;
using TheArchive.Loader;
using TheArchive.Utilities;
using UnityEngine;

namespace Hikaria.CGHUDInfo.Features
{
    [EnableFeatureByDefault]
    public class CGHUDInfo : Feature
    {
        public override string Name => "HUD信息增强";

        public override string Description => "使HUD信息更清晰易读";

        public override FeatureGroup Group => FeatureGroups.Accessibility;

        public static new ILocalizationService Localization { get; set; }

        [FeatureConfig]
        public static CGHUDInfoSettings Settings { get; set; }

        public class CGHUDInfoSettings
        {
            [FSDisplayName("显示栏位")]
            public List<HUDInfos> ShowSlots { get; set; } = new();

            [FSDisplayName("隐藏空栏位")]
            public bool HideEmptySlots { get; set; } = true;

            [FSDisplayName("瞄准时透明")]
            public bool TransparentWhenAim { get; set; } = true;

            [FSDisplayName("自动文本大小")]
            [FSDescription("跟据手持物品自动调整对应文本大小")]
            public bool AutoFontSizeUp { get; set; } = true;

            [FSDisplayName("自动隐藏文本")]
            [FSDescription("跟据手持物品自动隐藏无关内容")]
            public bool AutoHideText { get; set; } = true;

            [FSDisplayName("动态透明度")]
            public bool DynamicTrasparency { get; set; } = true;

            [FSDisplayName("信息常显")]
            public bool AlwaysVisible { get; set; } = true;

            [FSDisplayName("使用次数替代百分比")]
            public bool UseTimesInsteadOfPercentage { get; set; } = true;
        }

        [Localized]
        public enum HUDInfos
        {
            Health,
            Infection,
            GearStandard,
            GearSpecial,
            GearClass,
            ResourcePack,
            Consumable,
            BotLeader
        }

        public override void Init()
        {
            LoaderWrapper.ClassInjector.RegisterTypeInIl2Cpp<PlayerHudDistanceModifier>();
        }

        public override void OnEnable()
        {
            _sentryGunInstances.Clear();

            if (CurrentGameState < (int)eGameStateName.InLevel)
                return;

            foreach (var sgi in UnityEngine.Object.FindObjectsOfType<SentryGunInstance>())
            {
                var owner = sgi.Owner?.Owner;
                if (owner != null)
                {
                    _sentryGunInstances[owner.Lookup] = sgi;
                }
            }

            foreach (var agent in PlayerManager.PlayerAgentsInLevel)
            {
                if (!agent.IsLocallyOwned)
                {
                    if (agent.GetComponent<PlayerHudDistanceModifier>() == null)
                        agent.gameObject.AddComponent<PlayerHudDistanceModifier>();
                }
            }
        }

        public override void OnDisable()
        {
            foreach (var modifier in UnityEngine.Object.FindObjectsOfType<PlayerHudDistanceModifier>())
            {
                modifier.SafeDestroy();
            }

            _sentryGunInstances.Clear();
        }

        public override void OnFeatureSettingChanged(FeatureSetting setting)
        {
            foreach (var modifier in UnityEngine.Object.FindObjectsOfType<PlayerHudDistanceModifier>())
            {
                modifier.UpdateNavMarkerOnPlayer(true);
            }
        }

        private static RatioColorDeterminer _determinerHealth = new RatioColorDeterminer
        {
            _colorGrades = new List<RatioColor>
            {
                new RatioColor(Color.red, 0f),
                new RatioColor(Color.red, 0.2f),
                new RatioColor(Color.yellow, 0.5f),
                new RatioColor(Color.green, 0.8f),
                new RatioColor(Color.cyan, 1f)
            }
        };

        private static RatioColorDeterminer _determineAmmoWeaponRelInPack = new RatioColorDeterminer
        {
            _colorGrades = new List<RatioColor>
            {
                new RatioColor(Color.red, 0f),
                new RatioColor(Color.yellow, 0.5f),
                new RatioColor(Color.green, 0.8f),
                new RatioColor(Color.cyan, 1f)
            }
        };

        private static RatioColorDeterminer _detemineAmmoResourceRelInPack = new RatioColorDeterminer
        {
            _colorGrades = new List<RatioColor>
            {
                new RatioColor(ColorExt.Hex("FF8B47"), 0f),
                new RatioColor(Color.yellow, 0.2f),
                new RatioColor(Color.green, 0.4f),
                new RatioColor(Color.cyan, 1f)
            }
        };

        private static Dictionary<ulong, SentryGunInstance> _sentryGunInstances = new();

        [ArchivePatch(typeof(PlaceNavMarkerOnGO), nameof(PlaceNavMarkerOnGO.PlaceMarker))]
        private class PlaceNavMarkerOnGO__PlaceMarker__Patch
        {
            private static void Postfix(PlaceNavMarkerOnGO __instance)
            {
                if (__instance.m_marker != null && __instance.type == PlaceNavMarkerOnGO.eMarkerType.Player)
                {
                    __instance.m_marker.m_playerName.alignment = TMPro.TextAlignmentOptions.Bottom;
                    __instance.m_marker.m_playerName.fontSizeMax = 30f;
                    __instance.m_marker.m_playerName.fontSizeMin = 25f;
                }
            }
        }

        [ArchivePatch(typeof(PlayerAgent), nameof(PlayerAgent.Setup))]
        private class PlayerAgent__Setup__Patch
        {
            private static void Postfix(PlayerAgent __instance)
            {
                if (__instance.IsLocallyOwned)
                    return;
                if (__instance.GetComponent<PlayerHudDistanceModifier>() == null)
                    __instance.gameObject.AddComponent<PlayerHudDistanceModifier>();
            }
        }

        [ArchivePatch(typeof(SentryGunInstance), nameof(SentryGunInstance.OnSpawn))]
        private class SentryGunInstance__OnSpawn__Patch
        {
            private static void Postfix(SentryGunInstance __instance)
            {
                if (CurrentGameState < (int)eGameStateName.InLevel)
                    return;
                var owner = __instance.Owner?.Owner;
                if (owner == null || owner.IsLocal)
                    return;

                _sentryGunInstances[owner.Lookup] = __instance;
            }
        }

        [ArchivePatch(typeof(SentryGunInstance), nameof(SentryGunInstance.OnDestroy))]
        private class SentryGunInstance__OnDestroy__Patch
        {
            private static void Prefix(SentryGunInstance __instance)
            {
                if (CurrentGameState < (int)eGameStateName.InLevel)
                    return;
                var owner = __instance.Owner?.Owner;
                if (owner == null || owner.IsLocal)
                    return;

                _sentryGunInstances.Remove(owner.Lookup);
            }
        }

        [ArchivePatch(typeof(PlayerInventoryLocal), nameof(PlayerInventoryLocal.DoWieldItem))]
        private class PlayerInventoryLocal__DoWieldItem__Patch
        {
            private static void Postfix(PlayerInventoryLocal __instance)
            {
                PlaceNavMarkerOnGO__UpdateExtraInfo__Patch.UpdateSizeUp(__instance.Owner);
            }
        }

        [ArchivePatch(typeof(PlaceNavMarkerOnGO), nameof(PlaceNavMarkerOnGO.UpdateExtraInfo))]
        private class PlaceNavMarkerOnGO__UpdateExtraInfo__Patch
        {
            static bool healthSizeUp = false;
            static bool weaponSizeUp = false;
            static bool toolSizeUp = false;
            static bool infectionSizeUp = false;

            static bool hideHealth = false;
            static bool hideWeaponAmmo = false;
            static bool hideInfection = false;
            static bool hideToolAmmo = false;
            static bool hideOthers = false;

            enum eResourcePackType : byte
            {
                None,
                Health,
                AmmoWeapon,
                AmmoTool,
                Disinfection
            }

            static eResourcePackType currentResourcePack = eResourcePackType.None;

            static eResourcePackType GetPackType(eResourceContainerSpawnType spawnType)
            {
                return spawnType switch
                {
                    eResourceContainerSpawnType.Disinfection => eResourcePackType.Disinfection,
                    eResourceContainerSpawnType.Health => eResourcePackType.Health,
                    eResourceContainerSpawnType.AmmoTool => eResourcePackType.AmmoTool,
                    eResourceContainerSpawnType.AmmoWeapon => eResourcePackType.AmmoWeapon,
                    _ => eResourcePackType.None,
                };
            }

            public static void UpdateSizeUp(PlayerAgent playerAgent)
            {
                healthSizeUp = false;
                weaponSizeUp = false;
                toolSizeUp = false;
                infectionSizeUp = false;

                hideHealth = false;
                hideInfection = false;
                hideWeaponAmmo = false;
                hideToolAmmo = false;
                hideOthers = false;

                currentResourcePack = playerAgent.Inventory.WieldedSlot == InventorySlot.ResourcePack ? GetPackType(playerAgent.Inventory.WieldedItem.Cast<ResourcePackFirstPerson>().m_packType) : eResourcePackType.None;

                if (currentResourcePack != eResourcePackType.None)
                {
                    if (Settings.AutoHideText)
                    {
                        hideHealth = currentResourcePack != eResourcePackType.Health;
                        hideInfection = currentResourcePack != eResourcePackType.Disinfection;
                        hideWeaponAmmo = currentResourcePack != eResourcePackType.AmmoWeapon;
                        hideToolAmmo = currentResourcePack != eResourcePackType.AmmoTool;
                        hideOthers = true;
                    }

                    if (Settings.AutoFontSizeUp)
                    {
                        switch (currentResourcePack)
                        {
                            case eResourcePackType.Health:
                                healthSizeUp = true;
                                break;
                            case eResourcePackType.AmmoWeapon:
                                weaponSizeUp = true;
                                break;
                            case eResourcePackType.AmmoTool:
                                toolSizeUp = true;
                                break;
                            case eResourcePackType.Disinfection:
                                infectionSizeUp = true;
                                break;
                        }
                    }
                }
            }

            static StringBuilder sb = new(300);

            private static bool Prefix(PlaceNavMarkerOnGO __instance)
            {
                if (!__instance.m_hasPlayer || __instance.type != PlaceNavMarkerOnGO.eMarkerType.Player)
                    return ArchivePatch.RUN_OG;
                var playerAgent = __instance.Player;
                if (playerAgent == null || playerAgent.IsLocallyOwned)
                    return ArchivePatch.RUN_OG;
                var owner = playerAgent.Owner;
                if (owner == null)
                    return ArchivePatch.RUN_OG;
                var damageable = playerAgent.Damage;
                if (damageable == null)
                    return ArchivePatch.RUN_OG;

                if (!hideHealth && Settings.ShowSlots.Contains(HUDInfos.Health))
                {
                    var health = damageable.GetHealthRel();
                    sb.AppendLine($"<color=#{_determinerHealth.GetDeterminedColorHTML(health, 1f - damageable.Infection)}><size={(healthSizeUp ? "100" : "75")}%><u>{Localization.Get(1)} {health * 100f:N0}%</u></size></color>");
                    if (Localization.CurrentLanguage != TheArchive.Core.Localization.Language.English)
                    {
                        // 添加空白行避免下划线与文本重叠问题
                        sb.AppendLine($"<size={(healthSizeUp ? "50" : "37.5")}%> </size>");
                    }
                }

                if (!hideInfection && Settings.ShowSlots.Contains(HUDInfos.Infection))
                {
                    if (damageable.Infection > 0.1f)
                        sb.AppendLine($"<color=#00FFA8><size={(infectionSizeUp ? "100" : "70")}%>{Localization.Get(2)} {damageable.Infection * 100f:N0}%</size></color>");
                }

                var backpack = __instance.m_playerBackpack;
                if (backpack != null)
                {
                    var ammoStorage = backpack.AmmoStorage;
                    if (!hideWeaponAmmo && Settings.ShowSlots.Contains(HUDInfos.GearStandard) && backpack.TryGetBackpackItem(InventorySlot.GearStandard, out var backpackItem))
                    {
                        ItemEquippable itemEquippable = backpackItem.Instance.TryCast<ItemEquippable>();
                        if (itemEquippable != null)
                        {
                            if (itemEquippable.ItemDataBlock != null && itemEquippable.ItemDataBlock.GUIShowAmmoInfinite)
                            {
                                sb.AppendLine($"<size={(weaponSizeUp ? "100" : "70")}%><color=#{ColorUtility.ToHtmlStringRGBA(_determineAmmoWeaponRelInPack.GetDeterminedColor(100))}>{itemEquippable.ArchetypeName}</color></size>");
                            }
                            else
                            {
                                var standardAmmoRelInPack = ammoStorage.StandardAmmo.RelInPack;
                                sb.AppendLine($"<size={(weaponSizeUp ? "100" : "70")}%><color=#{ColorUtility.ToHtmlStringRGBA(_determineAmmoWeaponRelInPack.GetDeterminedColor(standardAmmoRelInPack))}>{itemEquippable.ArchetypeName} {standardAmmoRelInPack * 100f:N0}%</color>{(backpackItem.Status == eInventoryItemStatus.Deployed ? $" <color=red>[{Text.Get(2505980868U)}]</color>" : string.Empty)}</size>");
                            }
                        }
                    }

                    if (!hideWeaponAmmo && Settings.ShowSlots.Contains(HUDInfos.GearSpecial) && backpack.TryGetBackpackItem(InventorySlot.GearSpecial, out var backpackItem2))
                    {
                        ItemEquippable itemEquippable2 = backpackItem2.Instance.TryCast<ItemEquippable>();
                        if (itemEquippable2 != null)
                        {
                            if (itemEquippable2.ItemDataBlock != null && itemEquippable2.ItemDataBlock.GUIShowAmmoInfinite)
                            {
                                sb.AppendLine($"<size={(weaponSizeUp ? "100" : "70")}%><color=#{ColorUtility.ToHtmlStringRGBA(_determineAmmoWeaponRelInPack.GetDeterminedColor(100))}>{itemEquippable2.ArchetypeName}</color></size>");
                            }
                            else
                            {
                                var specialAmmoRelInPack = ammoStorage.SpecialAmmo.RelInPack;
                                sb.AppendLine($"<size={(weaponSizeUp ? "100" : "70")}%><color=#{ColorUtility.ToHtmlStringRGBA(_determineAmmoWeaponRelInPack.GetDeterminedColor(specialAmmoRelInPack))}>{itemEquippable2.ArchetypeName} {specialAmmoRelInPack * 100f:N0}%</color>{(backpackItem2.Status == eInventoryItemStatus.Deployed ? $" <color=red>[{Text.Get(2505980868U)}]</color>" : string.Empty)}</size>");
                            }
                        }
                    }

                    if (!hideToolAmmo && Settings.ShowSlots.Contains(HUDInfos.GearClass) && backpack.TryGetBackpackItem(InventorySlot.GearClass, out var backpackItem3) && backpackItem3 != null && backpackItem3.Instance != null)
                    {
                        ItemEquippable itemEquippable3 = backpackItem3.Instance.TryCast<ItemEquippable>();
                        if (itemEquippable3 != null)
                        {
                            string archetypeName = itemEquippable3.ArchetypeName;
                            var idRange = itemEquippable3.GearIDRange;
                            uint compID = idRange.GetCompID(eGearComponent.Category);
                            eWeaponFireMode compID2 = (eWeaponFireMode)idRange.GetCompID(eGearComponent.FireMode);
                            if (compID == 12U)
                            {
                                var block = SentryGunInstance_Firing_Bullets.GetArchetypeDataForFireMode(compID2);
                                if (block != null)
                                    archetypeName = block.PublicName;
                            }
                            if (itemEquippable3.ItemDataBlock != null && itemEquippable3.ItemDataBlock.GUIShowAmmoInfinite)
                            {
                                sb.AppendLine($"<size={(toolSizeUp ? "100" : "70")}%><color=#{ColorUtility.ToHtmlStringRGBA(ColorExt.Hex("FDA1FF"))}>{archetypeName}</color></size>");
                            }
                            else
                            {
                                float classAmmoRelInPack = ammoStorage.ClassAmmo.RelInPack;
                                if (_sentryGunInstances.TryGetValue(owner.Lookup, out var sentryGunInstance))
                                {
                                    classAmmoRelInPack = sentryGunInstance.Ammo / sentryGunInstance.AmmoMaxCap;
                                }
                                sb.AppendLine($"<size={(toolSizeUp ? "100" : "70")}%><color=#{_determineAmmoWeaponRelInPack.GetDeterminedColorHTML(classAmmoRelInPack)}>{archetypeName} {classAmmoRelInPack * 100f:N0}%</color>{(backpackItem3.Status == eInventoryItemStatus.Deployed ? $" <color=red>[{Text.Get(2505980868U)}]</color>" : string.Empty)}</size>");
                            }
                        }
                    }
                    if (!hideOthers && Settings.ShowSlots.Contains(HUDInfos.ResourcePack))
                    {
                        bool flag = false;
                        if (backpack.TryGetBackpackItem(InventorySlot.ResourcePack, out var backpackItem4) && backpackItem4 != null && backpackItem4.Instance != null)
                        {
                            ItemEquippable itemEquippable4 = backpackItem4.Instance.TryCast<ItemEquippable>();
                            if (itemEquippable4 != null)
                            {
                                if (itemEquippable4.ItemDataBlock != null && itemEquippable4.ItemDataBlock.GUIShowAmmoInfinite)
                                {
                                    sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGBA(_detemineAmmoResourceRelInPack.GetDeterminedColor(100))}>{itemEquippable4.ArchetypeName}</color>");
                                    flag = true;
                                }
                                else
                                {
                                    float resourceAmmo = Settings.UseTimesInsteadOfPercentage ? ammoStorage.ResourcePackAmmo.BulletsInPack : ammoStorage.ResourcePackAmmo.RelInPack * 100f;
                                    if (resourceAmmo > 0f)
                                    {
                                        sb.AppendLine($"<color=#{_detemineAmmoResourceRelInPack.GetDeterminedColorHTML(ammoStorage.ResourcePackAmmo.RelInPack)}>{itemEquippable4.ArchetypeName} {resourceAmmo:N0}{(Settings.UseTimesInsteadOfPercentage ? $" {Localization.Get(7)}{((Localization.CurrentLanguage == TheArchive.Core.Localization.Language.Chinese || resourceAmmo <= 1f) ? string.Empty : "s")}" : "%")}</color>{(backpackItem4.Status == eInventoryItemStatus.Deployed ? $" <color=red>[{Text.Get(2505980868U)}]</color>" : string.Empty)}");
                                        flag = true;
                                    }
                                }
                            }
                        }
                        if (!flag && !Settings.HideEmptySlots)
                            sb.AppendLine($"<color=#B3B3B3>{Localization.Get(3)}</color>");
                    }

                    if (!hideOthers && Settings.ShowSlots.Contains(HUDInfos.Consumable))
                    {
                        bool flag = false;
                        if (backpack.TryGetBackpackItem(InventorySlot.Consumable, out var backpackItem5) && backpackItem5 != null && backpackItem5.Instance != null)
                        {
                            ItemEquippable itemEquippable5 = backpackItem5.Instance.TryCast<ItemEquippable>();
                            if (itemEquippable5 != null)
                            {
                                if (itemEquippable5.ItemDataBlock != null && itemEquippable5.ItemDataBlock.GUIShowAmmoInfinite)
                                {
                                    sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGBA(_detemineAmmoResourceRelInPack.GetDeterminedColor(100))}>{itemEquippable5.ArchetypeName}</color>");
                                    flag = true;
                                }
                                else
                                {
                                    float consumableAmmo = Settings.UseTimesInsteadOfPercentage ? ammoStorage.ConsumableAmmo.BulletsInPack : ammoStorage.ConsumableAmmo.RelInPack * 100f;
                                    if (consumableAmmo > 0f)
                                    {
                                        sb.AppendLine($"<color=#{_detemineAmmoResourceRelInPack.GetDeterminedColorHTML(ammoStorage.ConsumableAmmo.RelInPack)}>{itemEquippable5.ArchetypeName} {consumableAmmo:N0}{(Settings.UseTimesInsteadOfPercentage ? $" {Localization.Get(7)}{((Localization.CurrentLanguage == TheArchive.Core.Localization.Language.Chinese || consumableAmmo <= 1f) ? string.Empty : "s")}" : "%")}</color>{(backpackItem5.Status == eInventoryItemStatus.Deployed ? $" <color=red>[{Text.Get(2505980868U)}]</color>" : string.Empty)}");
                                        flag = true;
                                    }
                                }
                            }
                        }
                        if (!flag && !Settings.HideEmptySlots)
                            sb.AppendLine($"<color=#B3B3B3>{Localization.Get(4)}</color>");
                    }
                }

                if (!hideOthers && Settings.ShowSlots.Contains(HUDInfos.BotLeader) && owner.IsBot)
                {
                    PlayerAIBot component = playerAgent.GetComponent<PlayerAIBot>();
                    if (component != null)
                    {
                        var leader = component.SyncValues.Leader;
                        if (leader != null)
                            sb.AppendLine($"{Localization.Get(5)} <color=#{ColorUtility.ToHtmlStringRGB(leader.Owner.PlayerColor)}>{(leader.IsLocallyOwned ? $"{Localization.Get(6)}" : leader.Owner.NickName)}</color>");
                    }
                }

                __instance.m_extraInfo = $"<color=#CCCCCC66><size=70%>{sb}</size></color>";
                sb.Clear();

                return ArchivePatch.SKIP_OG;
            }
        }


        private class PlayerHudDistanceModifier : MonoBehaviour
        {
            private void Awake()
            {
                m_owner = GetComponent<PlayerAgent>();
            }

            private void Update()
            {
                UpdateNavMarkerOnPlayer();
            }

            private void OnDestroy()
            {
                var placeNavMarkerOnGO = m_owner.NavMarker;
                var navMarker = placeNavMarkerOnGO?.m_marker;
                if (navMarker == null)
                    return;

                navMarker.transform.localScale = Vector3.one * 0.8412f;
                placeNavMarkerOnGO.m_isInfoDirty = true;
            }

            public void UpdateNavMarkerOnPlayer(bool manualUpdate = false)
            {
                var placeNavMarkerOnGO = m_owner.NavMarker;
                var navMarker = placeNavMarkerOnGO?.m_marker;
                if (navMarker == null)
                    return;

                if (manualUpdate)
                    placeNavMarkerOnGO.m_isInfoDirty = true;

                if (s_localPlayerAgent == null)
                {
                    s_localPlayerAgent = PlayerManager.GetLocalPlayerAgent();
                    navMarker.transform.localScale = Vector3.one * 0.8412f;
                    return;
                }

                float scale = Mathf.Clamp(Mathf.Clamp(Vector3.Distance(m_owner.Position, s_localPlayerAgent.Position), MIN_DISTANCE, MAX_DISTANCE) / MAX_DISTANCE, MIN_SIZE, MAX_SIZE);
                navMarker.transform.localScale = Vector3.one * scale;

                if (Settings.TransparentWhenAim && s_localPlayerAgent.Inventory.WieldedItem.AimButtonHeld && s_localPlayerAgent.Inventory.WieldedSlot >= InventorySlot.GearStandard && s_localPlayerAgent.Inventory.WieldedSlot <= InventorySlot.GearClass)
                {
                    navMarker.SetAlpha(MIN_ANGLE_ALPHA_VALUE);
                }
                else if (Settings.DynamicTrasparency)
                {
                    Vector3 dir = m_owner.EyePosition - s_localPlayerAgent.EyePosition;
                    float num2 = Vector3.Angle(s_localPlayerAgent.FPSCamera.CameraRayDir, dir);
                    num2 = Mathf.Clamp(num2, MIN_ANGLE_VALUE, MAX_ANGLE_VALUE);
                    float num3 = MIN_ANGLE_VALUE / num2;
                    navMarker.SetAlpha(Mathf.Clamp(num3, MIN_ANGLE_ALPHA_VALUE, 1f));
                }
                else
                {
                    navMarker.SetAlpha(1f);
                }

                if (Settings.AlwaysVisible)
                {
                    if (!placeNavMarkerOnGO.m_extraInfoVisible)
                    {
                        placeNavMarkerOnGO.m_extraInfoVisible = true;
                    }
                }
                else
                {
                    if (!placeNavMarkerOnGO.m_extraInfoVisible)
                    {
                        navMarker.transform.localScale = Vector3.one * 0.8412f;
                    }
                }
                placeNavMarkerOnGO.OnPlayerInfoUpdated(true);
            }

            private PlayerAgent m_owner;

            private static PlayerAgent s_localPlayerAgent;

            private readonly float MAX_SIZE = 15f;

            private readonly float MIN_SIZE = 1.5f;

            private readonly float MAX_DISTANCE = 40f;

            private readonly float MIN_DISTANCE = 10f;

            private readonly float MIN_ANGLE_ALPHA_VALUE = 0.075f;

            private readonly float MAX_ANGLE_VALUE = 40f;

            private readonly float MIN_ANGLE_VALUE = 8f;
        }
    }
}
