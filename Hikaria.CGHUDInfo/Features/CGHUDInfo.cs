using Hikaria.CGHUDInfo.Utils;
using Localization;
using Player;
using System.Text;
using TheArchive.Core.Attributes;
using TheArchive.Core.Attributes.Feature.Settings;
using TheArchive.Core.FeaturesAPI;
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
            public List<HUDInfos> ShowSlots { get; set; } = new()
            {
                HUDInfos.Health,
                HUDInfos.Infection,
                HUDInfos.GearStandard,
                HUDInfos.GearSpecial,
                HUDInfos.GearClass,
                HUDInfos.ResourcePack,
                HUDInfos.Consumable,
                HUDInfos.BotLeader
            };

            [FSDisplayName("隐藏空栏位")]
            public bool HideEmptySlots { get; set; } = false;

            [FSDisplayName("瞄准时透明")]
            public bool TransparentWhenAim { get; set; } = true;

            [FSDisplayName("信息常显")]
            public bool AlwaysVisible { get; set; } = true;
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
        }

        public override void OnDisable()
        {
            foreach (var modifier in UnityEngine.Object.FindObjectsOfType<PlayerHudDistanceModifier>())
            {
                modifier.SafeDestroy();
            }

            _sentryGunInstances.Clear();
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

        [ArchivePatch(typeof(PlaceNavMarkerOnGO), nameof(PlaceNavMarkerOnGO.UpdateExtraInfo))]
        private class PlaceNavMarkerOnGO__UpdateExtraInfo__Patch
        {
            private static bool Prefix(PlaceNavMarkerOnGO __instance)
            {
                if (!__instance.m_hasPlayer || __instance.type != PlaceNavMarkerOnGO.eMarkerType.Player)
                    return ArchivePatch.RUN_OG;

                var playerAgent = __instance.Player;
                if (playerAgent.IsLocallyOwned)
                    return ArchivePatch.RUN_OG;

                StringBuilder sb = new(300);

                var owner = playerAgent.Owner;
                var damageable = playerAgent.Damage;

                if (Settings.ShowSlots.Contains(HUDInfos.Health))
                {
                    var health = damageable.GetHealthRel();
                    sb.AppendLine($"<color=#{_determinerHealth.GetDeterminedColorHTML(health + damageable.Infection)}><size=80%><u>{Localization.Get(1)} {health * 100f:N0}%</u></size></color>");
                    if (Localization.CurrentLanguage != TheArchive.Core.Localization.Language.English)
                    {
                        // 添加空白行避免下划线与文本重叠问题
                        sb.AppendLine($"<size=40%> </size>");
                    }
                }

                if (Settings.ShowSlots.Contains(HUDInfos.Infection))
                {
                    if (damageable.Infection > 0.1f)
                        sb.AppendLine($"<color=#00FFA8><size=70%>{Localization.Get(2)} {damageable.Infection * 100f:N0}%</size></color>");
                }

                var backpack = __instance.m_playerBackpack;
                if (backpack != null)
                {
                    var ammoStorage = backpack.AmmoStorage;

                    if (Settings.ShowSlots.Contains(HUDInfos.GearStandard) && backpack.TryGetBackpackItem(InventorySlot.GearStandard, out var backpackItem))
                    {
                        ItemEquippable itemEquippable = backpackItem.Instance.TryCast<ItemEquippable>();
                        if (itemEquippable != null)
                        {
                            var standardAmmoRelInPack = ammoStorage.StandardAmmo.RelInPack;
                            sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGBA(_determineAmmoWeaponRelInPack.GetDeterminedColor(standardAmmoRelInPack))}>{itemEquippable.ArchetypeName} {standardAmmoRelInPack * 100f:N0}%</color>");
                        }
                    }

                    if (Settings.ShowSlots.Contains(HUDInfos.GearSpecial) && backpack.TryGetBackpackItem(InventorySlot.GearSpecial, out var backpackItem2))
                    {
                        ItemEquippable itemEquippable2 = backpackItem2.Instance.TryCast<ItemEquippable>();
                        if (itemEquippable2 != null)
                        {
                            var specialAmmoRelInPack = ammoStorage.SpecialAmmo.RelInPack;
                            sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGBA(_determineAmmoWeaponRelInPack.GetDeterminedColor(specialAmmoRelInPack))}>{itemEquippable2.ArchetypeName} {specialAmmoRelInPack * 100f:N0}%</color>");
                        }
                    }

                    if (Settings.ShowSlots.Contains(HUDInfos.GearClass) && backpack.TryGetBackpackItem(InventorySlot.GearClass, out var backpackItem3) && backpackItem3 != null && backpackItem3.Instance != null)
                    {
                        ItemEquippable itemEquippable3 = backpackItem3.Instance.TryCast<ItemEquippable>();
                        if (itemEquippable3 != null)
                        {
                            float classAmmoRelInPack = ammoStorage.ClassAmmo.RelInPack;
                            if (_sentryGunInstances.TryGetValue(owner.Lookup, out var sentryGunInstance))
                            {
                                classAmmoRelInPack = sentryGunInstance.Ammo / sentryGunInstance.AmmoMaxCap;
                            }
                            if (itemEquippable3.ItemDataBlock != null && itemEquippable3.ItemDataBlock.GUIShowAmmoInfinite)
                            {
                                sb.AppendLine($"<color=#{ColorUtility.ToHtmlStringRGBA(ColorExt.Hex("FDA1FF"))}>{itemEquippable3.ArchetypeName}</color>");
                            }
                            else
                            {
                                sb.AppendLine($"<color=#{_determineAmmoWeaponRelInPack.GetDeterminedColorHTML(classAmmoRelInPack)}>{itemEquippable3.ArchetypeName} {classAmmoRelInPack * 100f:N0}%</color>{(backpackItem3.Status == eInventoryItemStatus.Deployed ? $" <color=red>[{Text.Get(2505980868U)}]</color>" : string.Empty)}");
                            }
                        }
                    }

                    bool flag = false;
                    if (Settings.ShowSlots.Contains(HUDInfos.ResourcePack) && backpack.TryGetBackpackItem(InventorySlot.ResourcePack, out var backpackItem4) && backpackItem4 != null && backpackItem4.Instance != null)
                    {
                        ItemEquippable itemEquippable4 = backpackItem4.Instance.TryCast<ItemEquippable>();
                        if (itemEquippable4 != null)
                        {
                            float resourceAmmoRelInPack = ammoStorage.ResourcePackAmmo.RelInPack;
                            if (resourceAmmoRelInPack > 0f)
                            {
                                sb.AppendLine($"<color=#{_detemineAmmoResourceRelInPack.GetDeterminedColorHTML(resourceAmmoRelInPack)}>{itemEquippable4.ArchetypeName} {resourceAmmoRelInPack * 100f:N0}%</color>");
                                flag = true;
                            }
                        }
                    }
                    if (!flag && !Settings.HideEmptySlots)
                        sb.AppendLine($"<color=#B3B3B3>{Localization.Get(3)}</color>");


                    flag = false;
                    if (Settings.ShowSlots.Contains(HUDInfos.Consumable) && backpack.TryGetBackpackItem(InventorySlot.Consumable, out var backpackItem5) && backpackItem5 != null && backpackItem5.Instance != null)
                    {
                        ItemEquippable itemEquippable5 = backpackItem5.Instance.TryCast<ItemEquippable>();
                        if (itemEquippable5 != null)
                        {
                            float consumableAmmoRelInPack = ammoStorage.ConsumableAmmo.RelInPack;
                            if (consumableAmmoRelInPack > 0f)
                            {
                                sb.AppendLine($"<color=#{_detemineAmmoResourceRelInPack.GetDeterminedColorHTML(ammoStorage.ConsumableAmmo.RelInPack)}>{itemEquippable5.ArchetypeName} {ammoStorage.ConsumableAmmo.RelInPack * 100f:N0}%</color>");
                                flag = true;
                            }
                        }
                    }
                    if (!flag && !Settings.HideEmptySlots)
                        sb.AppendLine($"<color=#B3B3B3>{Localization.Get(4)}</color>");
                }

                if (Settings.ShowSlots.Contains(HUDInfos.BotLeader) && owner.IsBot)
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

            private void UpdateNavMarkerOnPlayer()
            {
                var placeNavMarkerOnGO = m_owner.NavMarker;
                var navMarker = placeNavMarkerOnGO?.m_marker;
                if (navMarker == null)
                    return;
                if (s_localPlayerAgent == null)
                {
                    s_localPlayerAgent = PlayerManager.GetLocalPlayerAgent();
                    navMarker.transform.localScale = Vector3.one * MIN_SIZE;
                    return;
                }

                float scale = Mathf.Clamp(Mathf.Clamp(Vector3.Distance(m_owner.Position, s_localPlayerAgent.Position), MIN_DISTANCE, MAX_DISTANCE) / MAX_DISTANCE, MIN_SIZE, MAX_SIZE);
                navMarker.transform.localScale = Vector3.one * scale;

                if (Settings.TransparentWhenAim && (s_localPlayerAgent.Inventory.WieldedItem?.AimButtonHeld ?? false))
                {
                    navMarker.SetAlpha(MIN_ANGLE_ALPHA_VALUE);
                }
                else
                {
                    Vector3 dir = m_owner.EyePosition - s_localPlayerAgent.EyePosition;
                    float num2 = Vector3.Angle(s_localPlayerAgent.FPSCamera.CameraRayDir, dir);
                    num2 = Mathf.Clamp(num2, MIN_ANGLE_VALUE, MAX_ANGLE_VALUE);
                    float num3 = MIN_ANGLE_VALUE / num2;
                    navMarker.SetAlpha(Mathf.Clamp(num3, MIN_ANGLE_ALPHA_VALUE, 1f));
                }

                if (Settings.AlwaysVisible && !placeNavMarkerOnGO.m_extraInfoVisible)
                    placeNavMarkerOnGO.SetExtraInfoVisible(true);
                else
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
