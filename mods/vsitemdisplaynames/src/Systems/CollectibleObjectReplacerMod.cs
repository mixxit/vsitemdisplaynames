using HarmonyLib;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace vsitemdisplaynames.src.Systems
{
    [HarmonyPatch]
    // original of this after VS updates is at:
    // https://github.com/anegostudios/vsapi/blob/a176dfee9e62bf16487434702cbbd463123857fc/Common/Collectible/Collectible.cs#L1304
    // last updated by tyron on 3rd of march 2022
    // the point of this is to allow a attribute for the displayname
    public sealed class CollectibleObjectReplacerMod : ModSystem
    {
        private readonly Harmony harmony;
        public CollectibleObjectReplacerMod()
        {
            harmony = new Harmony("CollectibleObjectReplacerMod");
            harmony.PatchAll();
        }

        public override void Start(ICoreAPI api)
        {
            harmony.PatchAll();
            base.Start(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            api.RegisterCommand("setdisplayname", "sets an item displayname", "", CmdSetDisplayName, "root");
            api.RegisterCommand("setdisplayname", "sets an item displayname", "", CmdSetDisplayName, "root");
        }

        private void CmdSetDisplayName(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (player.Entity.RightHandItemSlot == null || player.Entity.RightHandItemSlot.Itemstack == null || player.Entity.RightHandItemSlot.Itemstack.Attributes == null)
            {
                player.SendMessage(groupId, $"No item in right hand: ", EnumChatType.CommandError);
            }

            if (args.Length < 1)
            {
                ClearItemDisplayName(player.Entity.RightHandItemSlot);
                player.SendMessage(groupId, $"Cleared display name", EnumChatType.CommandSuccess);
                return;
            }

            string displayName = args.PopAll();
            player.SendMessage(groupId, $"Set item displayname to: '{displayName}'", EnumChatType.CommandSuccess);
            SetItemDisplayName(player.Entity.RightHandItemSlot, displayName);

            return;
        }

        private void SetItemDisplayName(ItemSlot itemSlot, string displayName)
        {
            itemSlot.Itemstack.Attributes.SetString("displayName", displayName);
            itemSlot.MarkDirty();
        }

        private void ClearItemDisplayName(ItemSlot itemSlot)
        {
            itemSlot.Itemstack.Attributes.RemoveAttribute("displayName");
            itemSlot.MarkDirty();
        }

        public override double ExecuteOrder()
        {
            /// Worldgen:
            /// - GenTerra: 0 
            /// - RockStrata: 0.1
            /// - Deposits: 0.2
            /// - Caves: 0.3
            /// - Blocklayers: 0.4
            /// Asset Loading
            /// - Json Overrides loader: 0.05
            /// - Load hardcoded mantle block: 0.1
            /// - Block and Item Loader: 0.2
            /// - Recipes (Smithing, Knapping, Clayforming, Grid recipes, Alloys) Loader: 1
            /// 
            return 1.1;
        }
    }

    // Implemnts a displayname attribute to replace the item name in the UI
    [HarmonyPatch(typeof(CollectibleObject), "GetHeldItemName")]
    public class CollectibleObject_GetHeldItemName
    {
        [HarmonyPrefix]
        public static bool Prefix(CollectibleObject __instance, ItemStack itemStack, ref string __result)
        {
            string type = __instance.ItemClass.Name();

            try
            {
                if (itemStack != null && itemStack.Attributes != null && itemStack.Attributes.HasAttribute("displayName"))
                {
                    __result = itemStack.Attributes["displayName"].ToString();
                    return false;
                }
            } catch (Exception e)
            {
                // Just fall back
            }

            __result = Lang.GetMatching(__instance.Code?.Domain + AssetLocation.LocationSeparator + type + "-" + __instance.Code?.Path);
            return false;
        }
    }

}
