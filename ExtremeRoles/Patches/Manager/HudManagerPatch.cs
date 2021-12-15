﻿using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using UnityEngine;

namespace ExtremeRoles.Patches.Manager
{
    [HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
    public class HudManagerUpdatePatch
    {
        public static void Prefix(HudManager __instance)
        {
            if (__instance.GameSettings != null)
            {
                __instance.GameSettings.fontSize = 1.2f;
            }
        }

    }
}
