using HarmonyLib;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
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
            api.RegisterCommand("setloretext", "sets an item loretext", "", CmdSetLoreText, "root");
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

        private void CmdSetLoreText(IServerPlayer player, int groupId, CmdArgs args)
        {
            if (player.Entity.RightHandItemSlot == null || player.Entity.RightHandItemSlot.Itemstack == null || player.Entity.RightHandItemSlot.Itemstack.Attributes == null)
            {
                player.SendMessage(groupId, $"No item in right hand: ", EnumChatType.CommandError);
            }

            if (args.Length < 1)
            {
                ClearItemLoreText(player.Entity.RightHandItemSlot);
                player.SendMessage(groupId, $"Cleared lore text", EnumChatType.CommandSuccess);
                return;
            }

            string loreText = args.PopAll();
            player.SendMessage(groupId, $"Set item lore text to: '{loreText}'", EnumChatType.CommandSuccess);
            SetItemLoreText(player.Entity.RightHandItemSlot, loreText);

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

        private void SetItemLoreText(ItemSlot itemSlot, string loreText)
        {
            itemSlot.Itemstack.Attributes.SetString("loreText", loreText);
            itemSlot.MarkDirty();
        }

        private void ClearItemLoreText(ItemSlot itemSlot)
        {
            itemSlot.Itemstack.Attributes.RemoveAttribute("loreText");
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

    // Implemnts a loreText attribute to add lore text in the UI
    [HarmonyPatch(typeof(CollectibleObject), "GetHeldItemInfo")]
    public class CollectibleObjectGetHeldItemInfo
    {
        [HarmonyPrefix]
        public static bool Prefix(CollectibleObject __instance, ItemSlot inSlot, StringBuilder dsc, IWorldAccessor world, bool withDebugInfo)
        {
            ItemStack stack = inSlot.Itemstack;

            string descLangCode = __instance.Code?.Domain + AssetLocation.LocationSeparator + __instance.ItemClass.ToString().ToLowerInvariant() + "desc-" + __instance.Code?.Path;
            string descText = Lang.GetMatching(descLangCode);
            if (descText == descLangCode) descText = "";
            else descText = descText + "\n";

            dsc.Append((withDebugInfo ? "Id: " + __instance.Id + "\n" : ""));
            dsc.Append((withDebugInfo ? "Code: " + __instance.Code + "\n" : ""));

            int durability = __instance.GetDurability(stack);

            if (durability > 1)
            {
                dsc.AppendLine(Lang.Get("Durability: {0} / {1}", stack.Attributes.GetInt("durability", durability), durability));
            }


            if (__instance.MiningSpeed != null && __instance.MiningSpeed.Count > 0)
            {
                dsc.AppendLine(Lang.Get("Tool Tier: {0}", __instance.ToolTier));

                dsc.Append(Lang.Get("item-tooltip-miningspeed"));
                int i = 0;
                foreach (var val in __instance.MiningSpeed)
                {
                    if (val.Value < 1.1) continue;

                    if (i > 0) dsc.Append(", ");
                    dsc.Append(Lang.Get(val.Key.ToString()) + " " + val.Value.ToString("#.#") + "x");
                    i++;
                }

                dsc.Append("\n");

            }

            if (IsBackPack(stack))
            {
                dsc.AppendLine(Lang.Get("Quantity Slots: {0}", QuantityBackPackSlots(stack)));
                ITreeAttribute backPackTree = stack.Attributes.GetTreeAttribute("backpack");
                if (backPackTree != null)
                {
                    bool didPrint = false;

                    ITreeAttribute slotsTree = backPackTree.GetTreeAttribute("slots");

                    foreach (var val in slotsTree)
                    {
                        ItemStack cstack = (ItemStack)val.Value?.GetValue();

                        if (cstack != null && cstack.StackSize > 0)
                        {
                            if (!didPrint)
                            {
                                dsc.AppendLine(Lang.Get("Contents: "));
                                didPrint = true;
                            }
                            cstack.ResolveBlockOrItem(world);
                            dsc.AppendLine("- " + cstack.StackSize + "x " + cstack.GetName());
                        }
                    }

                    if (!didPrint)
                    {
                        dsc.AppendLine(Lang.Get("Empty"));
                    }

                }
            }

            EntityPlayer entity = world.Side == EnumAppSide.Client ? (world as IClientWorldAccessor).Player.Entity : null;

            float spoilState = __instance.AppendPerishableInfoText(inSlot, dsc, world);

            FoodNutritionProperties nutriProps = __instance.GetNutritionProperties(world, stack, entity);
            if (nutriProps != null)
            {
                float satLossMul = GlobalConstants.FoodSpoilageSatLossMul(spoilState, stack, entity);
                float healthLossMul = GlobalConstants.FoodSpoilageHealthLossMul(spoilState, stack, entity);

                if (Math.Abs(nutriProps.Health * healthLossMul) > 0.001f)
                {
                    dsc.AppendLine(Lang.Get("When eaten: {0} sat, {1} hp", Math.Round(nutriProps.Satiety * satLossMul), nutriProps.Health * healthLossMul));
                }
                else
                {
                    dsc.AppendLine(Lang.Get("When eaten: {0} sat", Math.Round(nutriProps.Satiety * satLossMul)));
                }

                dsc.AppendLine(Lang.Get("Food Category: {0}", Lang.Get("foodcategory-" + nutriProps.FoodCategory.ToString().ToLowerInvariant())));
            }



            if (__instance.GrindingProps != null)
            {
                dsc.AppendLine(Lang.Get("When ground: Turns into {0}x {1}", __instance.GrindingProps.GroundStack.ResolvedItemstack.StackSize, __instance.GrindingProps.GroundStack.ResolvedItemstack.GetName()));
            }

            if (__instance.CrushingProps != null)
            {
                dsc.AppendLine(Lang.Get("When pulverized: Turns into {0}x {1}", __instance.CrushingProps.CrushedStack.ResolvedItemstack.StackSize, __instance.CrushingProps.CrushedStack.ResolvedItemstack.GetName()));
                dsc.AppendLine(Lang.Get("Requires Pulverizer tier: {0}", __instance.CrushingProps.HardnessTier));
            }

            if (__instance.GetAttackPower(stack) > 0.5f)
            {
                dsc.AppendLine(Lang.Get("Attack power: -{0} hp", __instance.GetAttackPower(stack).ToString("0.#")));
                dsc.AppendLine(Lang.Get("Attack tier: {0}", __instance.ToolTier));
            }

            if (__instance.GetAttackRange(stack) > GlobalConstants.DefaultAttackRange)
            {
                dsc.AppendLine(Lang.Get("Attack range: {0} m", __instance.GetAttackRange(stack).ToString("0.#")));
            }

            if (__instance.CombustibleProps != null)
            {
                string smelttype = __instance.CombustibleProps.SmeltingType.ToString().ToLowerInvariant();
                if (smelttype == "fire")
                {
                    // Custom for clay items - do not show firing temperature as that is irrelevant to Pit kilns

                    dsc.AppendLine(Lang.Get("itemdesc-fireinkiln"));
                }
                else
                {
                    if (__instance.CombustibleProps.BurnTemperature > 0)
                    {
                        dsc.AppendLine(Lang.Get("Burn temperature: {0}°C", __instance.CombustibleProps.BurnTemperature));
                        dsc.AppendLine(Lang.Get("Burn duration: {0}s", __instance.CombustibleProps.BurnDuration));
                    }

                    if (__instance.CombustibleProps.MeltingPoint > 0)
                    {
                        dsc.AppendLine(Lang.Get("game:smeltpoint-" + smelttype, __instance.CombustibleProps.MeltingPoint));
                    }
                }

                if (__instance.CombustibleProps.SmeltedStack?.ResolvedItemstack != null)
                {
                    int instacksize = __instance.CombustibleProps.SmeltedRatio;
                    int outstacksize = __instance.CombustibleProps.SmeltedStack.ResolvedItemstack.StackSize;


                    string str = instacksize == 1 ?
                        Lang.Get("game:smeltdesc-" + smelttype + "-singular", outstacksize, __instance.CombustibleProps.SmeltedStack.ResolvedItemstack.GetName()) :
                        Lang.Get("game:smeltdesc-" + smelttype + "-plural", instacksize, outstacksize, __instance.CombustibleProps.SmeltedStack.ResolvedItemstack.GetName())
                    ;

                    dsc.AppendLine(str);
                }
            }

            if (descText.Length > 0 && dsc.Length > 0) dsc.Append("\n");
            dsc.Append(descText);

            if (__instance.Attributes?["pigment"]?["color"].Exists == true)
            {
                dsc.AppendLine(Lang.Get("Pigment: {0}", Lang.Get(__instance.Attributes["pigment"]["name"].AsString())));
            }

            if (stack != null && stack.Attributes != null && stack.Attributes.HasAttribute("loreText"))
            {
                dsc.AppendLine(Lang.Get("Lore Text: {0}", stack.Attributes["loreText"]));
            }

            JsonObject obj = __instance.Attributes?["fertilizerProps"];
            if (obj != null && obj.Exists)
            {
                FertilizerProps props = obj.AsObject<FertilizerProps>();
                if (props != null)
                {
                    dsc.AppendLine(Lang.Get("Fertilizer: {0}% N, {1}% P, {2}% K", props.N, props.P, props.K));
                }
            }




            float temp = __instance.GetTemperature(world, stack);
            if (temp > 20)
            {
                dsc.AppendLine(Lang.Get("Temperature: {0}°C", (int)temp));
            }

            return false;
        }

        // Copy of method inside Collectible
        private static int QuantityBackPackSlots(ItemStack itemstack)
        {
            if (itemstack == null || itemstack.Collectible.Attributes == null) return 0;
            return itemstack.Collectible.Attributes["backpack"]["quantitySlots"].AsInt();
        }

        // Copy of method inside Collectible
        private static bool IsBackPack(ItemStack itemstack)
        {
            if (itemstack == null || itemstack.Collectible.Attributes == null) return false;
            return itemstack.Collectible.Attributes["backpack"]["quantitySlots"].AsInt() > 0;
        }
    }

}
