using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using RoR2;
using System;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace SqueakyItemMultiplier
{
    [BepInPlugin(PluginInfo.PluginGuid, PluginInfo.PluginName, PluginInfo.PluginVersion)]
    [BepInDependency(RiskOfOptionsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class SqueakyItemMultiplierPlugin : BaseUnityPlugin
    {
        private const string RiskOfOptionsGuid = "com.rune580.riskofoptions";

        private ConfigEntry<int> _itemMultiplier;
        private ConfigEntry<bool> _enableDebugLogging;
        private ConfigEntry<bool> _multiplyLunarItems;
        private ConfigEntry<bool> _multiplyVoidItems;
        private ConfigEntry<bool> _multiplyTemporaryItems;
        private ConfigEntry<bool> _exponentialItems;

        private readonly GrantScaling _grantScaling = new GrantScaling();

        private Inventory _activePickupInventory;
        private bool _scalingInventoryGrant;

        public void Awake()
        {
            Logger.LogWarning("=== SQUEAKY ITEM MULTIPLIER AWAKE START ===");

            // Config setup
            _itemMultiplier = Config.Bind("Settings", "ItemMultiplier", 5,
                new ConfigDescription("Multiplier for items (e.g., 5 means 1 item becomes 5)",
                    new AcceptableValueRange<int>(1, int.MaxValue)));

            _enableDebugLogging = Config.Bind("Debug", "EnableDebugLogging", true,
                "Enable detailed logging");

            _multiplyLunarItems = Config.Bind("Settings", "MultiplyLunarItems", true,
                "Multiply lunar (blue) items");

            _multiplyVoidItems = Config.Bind("Settings", "MultiplyVoidItems", true,
                "Multiply void (purple) items");

            _multiplyTemporaryItems = Config.Bind("Settings", "MultiplyTemporaryItems", false,
                "Multiply temporary items while keeping the additional stacks temporary");

            _exponentialItems = Config.Bind("Settings", "ExponentialItems", false,
                "Grow grants per player and item type: at multiplier 5, successive pickups grant 5, 25, 125 copies. " +
                "Only eligible pickups while this setting is enabled advance the sequence; resets each run.");

            RegisterRiskOfOptionsIfAvailable();

            Logger.LogWarning($"=== CONFIG LOADED: Multiplier={_itemMultiplier.Value}, " +
                              $"TemporaryItems={_multiplyTemporaryItems.Value}, Debug={_enableDebugLogging.Value} ===");
            Logger.LogInfo(
                $"{PluginInfo.PluginName} v{PluginInfo.PluginVersion} loaded! Multiplier: {_itemMultiplier.Value}x");

            // Hook at the pickup level - where items are granted from world pickups
            On.RoR2.GenericPickupController.AttemptGrant += OnPickupAttemptGrant;
            On.RoR2.Inventory.GiveItemPermanent_ItemIndex_int += OnGiveItemPermanent;
            On.RoR2.Inventory.GiveItemTemp += OnGiveItemTemporary;
            Run.onRunStartGlobal += ResetPickupProgression;
            Run.onRunDestroyGlobal += ResetPickupProgression;

            Logger.LogWarning("=== HOOK REGISTERED ===");
        }

        private void RegisterRiskOfOptionsIfAvailable()
        {
            if (!Chainloader.PluginInfos.ContainsKey(RiskOfOptionsGuid))
                return;

            try
            {
                RegisterRiskOfOptions();
                Logger.LogInfo("Registered settings with RiskOfOptions.");
            }
            catch (Exception exception)
            {
                Logger.LogWarning($"RiskOfOptions is installed, but its settings could not be registered: {exception}");
            }
        }

        // Keep the optional integration isolated from the core plugin load path.
        [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
        private void RegisterRiskOfOptions()
        {
            var riskOfOptionsAssembly = Chainloader.PluginInfos[RiskOfOptionsGuid].Instance.GetType().Assembly;
            var managerType = riskOfOptionsAssembly.GetType("RiskOfOptions.ModSettingsManager", true);
            var baseOptionType = riskOfOptionsAssembly.GetType("RiskOfOptions.Options.BaseOption", true);
            var intFieldOptionType = riskOfOptionsAssembly.GetType("RiskOfOptions.Options.IntFieldOption", true);
            var checkBoxOptionType = riskOfOptionsAssembly.GetType("RiskOfOptions.Options.CheckBoxOption", true);

            var setDescription = managerType.GetMethod("SetModDescription",
                new[] { typeof(string), typeof(string), typeof(string) });
            var addOption = managerType.GetMethod("AddOption",
                new[] { baseOptionType, typeof(string), typeof(string) });

            if (setDescription == null || addOption == null)
                throw new MissingMethodException(
                    "The installed RiskOfOptions version does not expose the expected API.");

            setDescription.Invoke(null, new object[]
            {
                "Configure how many copies of picked-up items are granted and which item types are affected.",
                PluginInfo.PluginGuid,
                PluginInfo.PluginName
            });

            AddRiskOfOptionsEntry(addOption, intFieldOptionType, _itemMultiplier);
            AddRiskOfOptionsEntry(addOption, checkBoxOptionType, _multiplyLunarItems);
            AddRiskOfOptionsEntry(addOption, checkBoxOptionType, _multiplyVoidItems);
            AddRiskOfOptionsEntry(addOption, checkBoxOptionType, _multiplyTemporaryItems);
            AddRiskOfOptionsEntry(addOption, checkBoxOptionType, _exponentialItems);
            AddRiskOfOptionsEntry(addOption, checkBoxOptionType, _enableDebugLogging);
        }

        private static void AddRiskOfOptionsEntry(System.Reflection.MethodInfo addOption, Type optionType,
            ConfigEntryBase configEntry)
        {
            var option = Activator.CreateInstance(optionType, configEntry);
            addOption.Invoke(null, new[] { option, PluginInfo.PluginGuid, PluginInfo.PluginName });
        }

        private void OnPickupAttemptGrant(On.RoR2.GenericPickupController.orig_AttemptGrant orig,
            GenericPickupController self, CharacterBody body)
        {
            if (body == null || self == null || body.inventory == null || _itemMultiplier.Value <= 1)
            {
                orig(self, body);
                return;
            }

            // Only run on server/authoritative machine
            // Clients don't need to process pickups - network sync handles everything
            if (!NetworkServer.active)
            {
                orig(self, body);
                return;
            }

            var previousPickupInventory = _activePickupInventory;
            _activePickupInventory = body.inventory;
            try
            {
                orig(self, body);
            }
            finally
            {
                _activePickupInventory = previousPickupInventory;
            }
        }

        private void OnGiveItemPermanent(On.RoR2.Inventory.orig_GiveItemPermanent_ItemIndex_int orig,
            Inventory self, ItemIndex itemIndex, int count)
        {
            if (!ShouldScaleGrant(self, itemIndex, count > 0))
            {
                orig(self, itemIndex, count);
                return;
            }

            var exponential = _exponentialItems.Value;
            var factor = _grantScaling.GetFactor(self, (int)itemIndex, _itemMultiplier.Value, exponential);
            var scaledCount = GrantScaling.ScalePermanent(count, self.GetItemCountEffective(itemIndex), factor);
            GrantWithoutRescaling(() => orig(self, itemIndex, scaledCount));
            if (exponential && scaledCount > 0)
                _grantScaling.RecordPickup(self, (int)itemIndex);
            LogScaledGrant(itemIndex, count, scaledCount, false);
        }

        private void OnGiveItemTemporary(On.RoR2.Inventory.orig_GiveItemTemp orig,
            Inventory self, ItemIndex itemIndex, float countToAdd)
        {
            if (!_multiplyTemporaryItems.Value || !ShouldScaleGrant(self, itemIndex, countToAdd > 0f) ||
                float.IsNaN(countToAdd) || float.IsInfinity(countToAdd))
            {
                orig(self, itemIndex, countToAdd);
                return;
            }

            var exponential = _exponentialItems.Value;
            var factor = _grantScaling.GetFactor(self, (int)itemIndex, _itemMultiplier.Value, exponential);
            var scaledCount = GrantScaling.ScaleTemporary(countToAdd, self.GetItemCountEffective(itemIndex), factor);
            if (scaledCount <= 0f)
                return;
            GrantWithoutRescaling(() => orig(self, itemIndex, scaledCount));
            if (exponential)
                _grantScaling.RecordPickup(self, (int)itemIndex);
            LogScaledGrant(itemIndex, countToAdd, scaledCount, true);
        }

        private bool ShouldScaleGrant(Inventory inventory, ItemIndex itemIndex, bool hasPositiveGrant)
        {
            if (!hasPositiveGrant || _scalingInventoryGrant || _activePickupInventory == null ||
                inventory != _activePickupInventory || _itemMultiplier.Value <= 1)
                return false;

            return ShouldMultiplyItem(ItemCatalog.GetItemDef(itemIndex));
        }

        private void ResetPickupProgression(Run run)
        {
            _grantScaling.Reset();
        }

        private void GrantWithoutRescaling(Action grant)
        {
            var wasScalingInventoryGrant = _scalingInventoryGrant;
            _scalingInventoryGrant = true;
            try
            {
                grant();
            }
            finally
            {
                _scalingInventoryGrant = wasScalingInventoryGrant;
            }
        }

        private void LogScaledGrant(ItemIndex itemIndex, object originalCount, object scaledCount, bool isTemporary)
        {
            if (!_enableDebugLogging.Value)
                return;

            var itemDef = ItemCatalog.GetItemDef(itemIndex);
            var lifetime = isTemporary ? "temporary" : "permanent";
            var itemName = itemDef != null ? itemDef.nameToken : itemIndex.ToString();
            Logger.LogInfo($"Multiplied {lifetime} grant for {itemName}: " +
                           $"{originalCount} -> {scaledCount}");
        }

        private bool ShouldMultiplyItem(ItemDef itemDef)
        {
            if (itemDef == null || itemDef.tier == ItemTier.NoTier)
                return false;

            // Filter lunar items
            if (itemDef.tier == ItemTier.Lunar && !_multiplyLunarItems.Value)
                return false;

            // Filter void items
            if ((itemDef.tier == ItemTier.VoidTier1 || itemDef.tier == ItemTier.VoidTier2 ||
                 itemDef.tier == ItemTier.VoidTier3 || itemDef.tier == ItemTier.VoidBoss) &&
                !_multiplyVoidItems.Value)
                return false;

            // Exclude scrap and world-unique items - not sure if working correctly
            return !itemDef.ContainsTag(ItemTag.Scrap) && !itemDef.ContainsTag(ItemTag.WorldUnique);
        }

        public void OnDestroy()
        {
            On.RoR2.GenericPickupController.AttemptGrant -= OnPickupAttemptGrant;
            On.RoR2.Inventory.GiveItemPermanent_ItemIndex_int -= OnGiveItemPermanent;
            On.RoR2.Inventory.GiveItemTemp -= OnGiveItemTemporary;
            Run.onRunStartGlobal -= ResetPickupProgression;
            Run.onRunDestroyGlobal -= ResetPickupProgression;
            _grantScaling.Reset();
            Logger.LogInfo($"{PluginInfo.PluginName} unloaded.");
        }
    }
}