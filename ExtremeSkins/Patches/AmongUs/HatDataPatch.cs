﻿using ExtremeSkins.Module;
using ExtremeSkins.SkinManager;

using Innersloth.Assets;

using HarmonyLib;

namespace ExtremeSkins.Patches.AmongUs;

#if WITHHAT

[HarmonyPatch(typeof(HatData), nameof(HatData.CreateAddressableAsset))]
public static class HatDataCreateAddressableAssetPatch
{
	public static bool Prefix(HatData __instance, ref AddressableAsset<HatViewData> __result)
	{
		if (ExtremeHatManager.HatData.TryGetValue(__instance.ProductId, out var value))
		{
			var asset = new HatAddressableAsset();
			asset.Init(value);
			__result = asset.Cast<AddressableAsset<HatViewData>>();
			return false;
		}
		return true;
	}
}
#endif