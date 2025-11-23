using BepInEx;
using BepInEx.Configuration;
using RoR2;
using UnityEngine.Networking;

namespace SqueakyItemMultiplier
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class SqueakyItemMultiplierPlugin : BaseUnityPlugin
    {
        private ConfigEntry<int> itemMultiplier;
        private ConfigEntry<bool> enableDebugLogging;
        private ConfigEntry<bool> multiplyLunarItems;
        private ConfigEntry<bool> multiplyVoidItems;

        public void Awake()
        {
            Logger.LogWarning("=== SQUEAKY ITEM MULTIPLIER AWAKE START ===");

            // Config setup
            itemMultiplier = Config.Bind("Settings", "ItemMultiplier", 5,
                new ConfigDescription("Multiplier for items (e.g., 5 means 1 item becomes 5)",
                    new AcceptableValueRange<int>(1, 100)));

            enableDebugLogging = Config.Bind("Debug", "EnableDebugLogging", true,
                "Enable detailed logging");

            multiplyLunarItems = Config.Bind("Settings", "MultiplyLunarItems", true,
                "Multiply lunar (blue) items");

            multiplyVoidItems = Config.Bind("Settings", "MultiplyVoidItems", true,
                "Multiply void (purple) items");

            Logger.LogWarning($"=== CONFIG LOADED: Multiplier={itemMultiplier.Value}, Debug={enableDebugLogging.Value} ===");
            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION} loaded! Multiplier: {itemMultiplier.Value}x");

            // Hook at the pickup level - where items are granted from world pickups
            On.RoR2.GenericPickupController.AttemptGrant += OnPickupAttemptGrant;

            Logger.LogWarning("=== HOOK REGISTERED ===");
        }

        private void OnPickupAttemptGrant(On.RoR2.GenericPickupController.orig_AttemptGrant orig, GenericPickupController self, CharacterBody body)
        {
            Logger.LogWarning($"=== PICKUP ATTEMPT GRANT CALLED ===");

            if (body == null || self == null || body.inventory == null || itemMultiplier.Value <= 1)
            {
                orig(self, body);
                return;
            }

            // Only run on server/authoritative machine
            // Clients don't need to process pickups - network sync handles everything
            if (!NetworkServer.active)
            {
                Logger.LogInfo("Not server, skipping");
                return;
            }

            // Take a snapshot of current items before granting
            var inventory = body.inventory;
            var itemCountsBefore = new System.Collections.Generic.Dictionary<ItemIndex, int>();

            foreach (ItemIndex itemIndex in ItemCatalog.allItems)
            {
                int count = inventory.GetItemCountEffective(itemIndex);
                if (count > 0)
                {
                    itemCountsBefore[itemIndex] = count;
                }
            }

            // Grant the original pickup
            orig(self, body);

            // Find what was added by comparing before/after
            foreach (ItemIndex itemIndex in ItemCatalog.allItems)
            {
                int currentCount = inventory.GetItemCountEffective(itemIndex);
                int previousCount = itemCountsBefore.ContainsKey(itemIndex) ? itemCountsBefore[itemIndex] : 0;
                int addedCount = currentCount - previousCount;

                if (addedCount > 0)
                {
                    var itemDef = ItemCatalog.GetItemDef(itemIndex);
                    Logger.LogWarning($"=== PICKUP DETECTED: {itemDef?.name ?? "unknown"}, added {addedCount} ===");

                    // Check if we should multiply this item
                    if (ShouldMultiplyItem(itemDef))
                    {
                        int extraCopies = addedCount * (itemMultiplier.Value - 1);
                        Logger.LogWarning($"=== MULTIPLYING: Giving {extraCopies} extra copies ===");
                        inventory.GiveItemPermanent(itemDef, extraCopies);

                        if (enableDebugLogging.Value)
                        {
                            Logger.LogInfo($"Multiplied {itemDef.nameToken} x{itemMultiplier.Value}");
                        }
                    }
                    else
                    {
                        Logger.LogInfo($"Item {itemDef?.name} filtered out");
                    }
                }
            }
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
            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} unloaded.");
        }
    }
}
