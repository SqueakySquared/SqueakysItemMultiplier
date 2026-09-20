using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using RoR2;
using System;
using System.Runtime.CompilerServices;
using UnityEngine.Networking;

namespace SqueakyItemMultiplier
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency(RiskOfOptionsGuid, BepInDependency.DependencyFlags.SoftDependency)]
    public class SqueakyItemMultiplierPlugin : BaseUnityPlugin
    {
        private const string RiskOfOptionsGuid = "com.rune580.riskofoptions";

        private ConfigEntry<int> itemMultiplier;
        private ConfigEntry<bool> enableDebugLogging;
        private ConfigEntry<bool> multiplyLunarItems;
        private ConfigEntry<bool> multiplyVoidItems;
        private ConfigEntry<bool> multiplyTemporaryItems;

        private Inventory activePickupInventory;
        private bool scalingInventoryGrant;

        public void Awake()
        {
            Logger.LogWarning("=== SQUEAKY ITEM MULTIPLIER AWAKE START ===");

            // Config setup
            itemMultiplier = Config.Bind("Settings", "ItemMultiplier", 5,
                new ConfigDescription("Multiplier for items (e.g., 5 means 1 item becomes 5)",
                    new AcceptableValueRange<int>(1, int.MaxValue)));

            enableDebugLogging = Config.Bind("Debug", "EnableDebugLogging", true,
                "Enable detailed logging");

            multiplyLunarItems = Config.Bind("Settings", "MultiplyLunarItems", true,
                "Multiply lunar (blue) items");

            multiplyVoidItems = Config.Bind("Settings", "MultiplyVoidItems", true,
                "Multiply void (purple) items");

            multiplyTemporaryItems = Config.Bind("Settings", "MultiplyTemporaryItems", false,
                "Multiply temporary items while keeping the additional stacks temporary");

            RegisterRiskOfOptionsIfAvailable();

            Logger.LogWarning($"=== CONFIG LOADED: Multiplier={itemMultiplier.Value}, " +
                              $"TemporaryItems={multiplyTemporaryItems.Value}, Debug={enableDebugLogging.Value} ===");
            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded! Multiplier: {itemMultiplier.Value}x");

            // Hook at the pickup level - where items are granted from world pickups
            On.RoR2.GenericPickupController.AttemptGrant += OnPickupAttemptGrant;
            On.RoR2.Inventory.GiveItemPermanent_ItemIndex_int += OnGiveItemPermanent;
            On.RoR2.Inventory.GiveItemTemp += OnGiveItemTemporary;

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
                throw new MissingMethodException("The installed RiskOfOptions version does not expose the expected API.");

            setDescription.Invoke(null, new object[]
            {
                "Configure how many copies of picked-up items are granted and which item types are affected.",
                PluginInfo.PLUGIN_GUID,
                PluginInfo.PLUGIN_NAME
            });

            AddRiskOfOptionsEntry(addOption, intFieldOptionType, itemMultiplier);
            AddRiskOfOptionsEntry(addOption, checkBoxOptionType, multiplyLunarItems);
            AddRiskOfOptionsEntry(addOption, checkBoxOptionType, multiplyVoidItems);
            AddRiskOfOptionsEntry(addOption, checkBoxOptionType, multiplyTemporaryItems);
            AddRiskOfOptionsEntry(addOption, checkBoxOptionType, enableDebugLogging);
        }

        private static void AddRiskOfOptionsEntry(System.Reflection.MethodInfo addOption, Type optionType,
            ConfigEntryBase configEntry)
        {
            var option = Activator.CreateInstance(optionType, configEntry);
            addOption.Invoke(null, new[] { option, PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME });
        }

        private void OnPickupAttemptGrant(On.RoR2.GenericPickupController.orig_AttemptGrant orig, GenericPickupController self, CharacterBody body)
        {
            if (body == null || self == null || body.inventory == null || itemMultiplier.Value <= 1)
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

            var previousPickupInventory = activePickupInventory;
            activePickupInventory = body.inventory;
            try
            {
                orig(self, body);
            }
            finally
            {
                activePickupInventory = previousPickupInventory;
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

            int scaledCount = CalculateScaledGrant(count, self.GetItemCountEffective(itemIndex));
            GrantWithoutRescaling(() => orig(self, itemIndex, scaledCount));
            LogScaledGrant(itemIndex, count, scaledCount, false);
        }

        private void OnGiveItemTemporary(On.RoR2.Inventory.orig_GiveItemTemp orig,
            Inventory self, ItemIndex itemIndex, float countToAdd)
        {
            if (!multiplyTemporaryItems.Value || !ShouldScaleGrant(self, itemIndex, countToAdd > 0f) ||
                float.IsNaN(countToAdd) || float.IsInfinity(countToAdd))
            {
                orig(self, itemIndex, countToAdd);
                return;
            }

            float scaledCount = CalculateScaledGrant(countToAdd, self.GetItemCountEffective(itemIndex));
            GrantWithoutRescaling(() => orig(self, itemIndex, scaledCount));
            LogScaledGrant(itemIndex, countToAdd, scaledCount, true);
        }

        private bool ShouldScaleGrant(Inventory inventory, ItemIndex itemIndex, bool hasPositiveGrant)
        {
            if (!hasPositiveGrant || scalingInventoryGrant || activePickupInventory == null ||
                inventory != activePickupInventory || itemMultiplier.Value <= 1)
                return false;

            return ShouldMultiplyItem(ItemCatalog.GetItemDef(itemIndex));
        }

        private int CalculateScaledGrant(int originalCount, int currentEffectiveCount)
        {
            long requestedCount = (long)originalCount * itemMultiplier.Value;
            long availableRoom = Math.Min(int.MaxValue,
                Math.Max(0L, (long)int.MaxValue - currentEffectiveCount));
            long safeLimit = Math.Max(originalCount, availableRoom);
            return (int)Math.Min(requestedCount, safeLimit);
        }

        private float CalculateScaledGrant(float originalCount, int currentEffectiveCount)
        {
            double requestedCount = originalCount * (double)itemMultiplier.Value;
            double availableRoom = Math.Min(int.MaxValue,
                Math.Max(0d, (double)int.MaxValue - currentEffectiveCount));
            double safeLimit = Math.Max(originalCount, availableRoom);
            return (float)Math.Min(requestedCount, safeLimit);
        }

        private void GrantWithoutRescaling(Action grant)
        {
            bool wasScalingInventoryGrant = scalingInventoryGrant;
            scalingInventoryGrant = true;
            try
            {
                grant();
            }
            finally
            {
                scalingInventoryGrant = wasScalingInventoryGrant;
            }
        }

        private void LogScaledGrant(ItemIndex itemIndex, object originalCount, object scaledCount, bool isTemporary)
        {
            if (!enableDebugLogging.Value)
                return;

            var itemDef = ItemCatalog.GetItemDef(itemIndex);
            string lifetime = isTemporary ? "temporary" : "permanent";
            Logger.LogInfo($"Multiplied {lifetime} grant for {itemDef?.nameToken ?? itemIndex.ToString()}: " +
                           $"{originalCount} -> {scaledCount}");
        }

        private bool ShouldMultiplyItem(ItemDef itemDef)
        {
            if (itemDef == null || itemDef.tier == ItemTier.NoTier)
                return false;

            // Filter lunar items
            if (itemDef.tier == ItemTier.Lunar && !multiplyLunarItems.Value)
                return false;

            // Filter void items
            if ((itemDef.tier == ItemTier.VoidTier1 || itemDef.tier == ItemTier.VoidTier2 ||
                 itemDef.tier == ItemTier.VoidTier3 || itemDef.tier == ItemTier.VoidBoss) &&
                !multiplyVoidItems.Value)
                return false;

            // Exclude scrap and world-unique items - not sure if working correctly
            return !itemDef.ContainsTag(ItemTag.Scrap) && !itemDef.ContainsTag(ItemTag.WorldUnique);
        }

        public void OnDestroy()
        {
            On.RoR2.GenericPickupController.AttemptGrant -= OnPickupAttemptGrant;
            On.RoR2.Inventory.GiveItemPermanent_ItemIndex_int -= OnGiveItemPermanent;
            On.RoR2.Inventory.GiveItemTemp -= OnGiveItemTemporary;
            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} unloaded.");
        }
    }
}
