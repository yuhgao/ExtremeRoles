﻿using System;
using System.Collections.Generic;
using System.Linq;

using HarmonyLib;
using Hazel;

using ExtremeRoles.Roles;

namespace ExtremeRoles.Patches.Manager
{
    [HarmonyPatch(typeof(RoleManager), nameof(RoleManager.SelectRoles))]
    class RoleManagerSelectRolesPatch
    {
        public static void Postfix()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)CustomRPC.GameInit, Hazel.SendOption.Reliable, -1);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            ExtremeRoleRPC.GameInit();

            PlayerControl[] playeres = PlayerControl.AllPlayerControls.ToArray();
            RoleAssignmentData extremeRolesData = CreateRoleData();
            var playerIndexList = Enumerable.Range(0, playeres.Count()).ToList();

            CombinationExtremeRoleAssign(
                ref extremeRolesData, ref playerIndexList);
            NormalExtremeRoleAssign(
                extremeRolesData, playerIndexList);
        }

        private static void CombinationExtremeRoleAssign(
            ref RoleAssignmentData extremeRolesData,
            ref List<int> shuffledArange)
        {

            Modules.Helpers.DebugLog($"NotAssignPlayerNum:{shuffledArange.Count}");

            var roleShuffleGen = new Random(
                UnityEngine.SystemInfo.processorFrequency);
            List<(List<MultiAssignRoleAbs>, (int, int))> roleData = extremeRolesData.CombinationRole.OrderBy(
                item => roleShuffleGen.Next()).ToList();

            var cloneRoledata = roleData;

            if (roleData.Count == 0) { return; }

            List<int> tempList = new List<int>(
                shuffledArange.OrderBy(item => roleShuffleGen.Next()).ToList());

            List<MultiAssignRoleAbs> removeRole = new List<MultiAssignRoleAbs>();

            do
            {

                for (int i = 0; i < roleData.Count; ++i)
                {
                    var (roles, (num, spawnRate)) = roleData[i];

                    if (!IsRoleSpawn(num, spawnRate)) { continue; }

                    int playerIndex = tempList[
                        UnityEngine.Random.Range(0, tempList.Count)];
                    tempList.Remove(playerIndex);
                    Modules.Helpers.DebugLog($"PlayerIndex:{playerIndex}");
                                
                    var tempRoles = new List<MultiAssignRoleAbs>(roles);
                    
                    foreach (var extremeRole in roles)
                    {

                        PlayerControl player = PlayerControl.AllPlayerControls[playerIndex];
                        bool result = false;
                        result = IsMultiAssignRole(
                            extremeRole, player);

                        if (result)
                        {
                            SetRoleToPlayer(
                                player, extremeRole.BytedRoleId, true);

                            removeRole.Add(
                                (MultiAssignRoleAbs)extremeRole.Clone());
                            tempRoles.Remove(extremeRole);

                            num = num - 1;
                            roleData[i] = (tempRoles, (num, spawnRate));

                            if (!extremeRole.CanHasAnotherRole || 
                                (player.Data.Role.Role == RoleTypes.Crewmate || 
                                 player.Data.Role.Role == RoleTypes.Impostor))
                            {
                                shuffledArange.Remove(playerIndex);
                            }
                        }
                    }
                    if (num > 0 && tempRoles.Count == 0)
                    {
                        roleData[i] = (
                            new List<MultiAssignRoleAbs>(removeRole),
                            (num, spawnRate));
                        removeRole.Clear();
                    }
                }
            } while (
                tempList.Count != 0 && !roleData.All(value => value.Item2.Item1 <= 0));
        }

        private static Tuple<List<int>, List<int>> SeparatePlayerType(
            List<int> playerIndexList)
        {
            List<int> impostersList = new List<int>();
            List<int> crewList = new List<int>();

            foreach (int index in playerIndexList)
            {
                RoleTypes role = PlayerControl.AllPlayerControls[index].Data.Role.Role;

                switch (role)
                {
                    case RoleTypes.Impostor:
                    case RoleTypes.Shapeshifter:
                        impostersList.Add(index);
                        break;
                    case RoleTypes.Crewmate:
                    case RoleTypes.Engineer:
                    case RoleTypes.Scientist:
                        crewList.Add(index);
                        break;
                    default:
                        throw new System.Exception("UnKnown Role Detect!!");
                }
            };

            return Tuple.Create(impostersList, crewList);
        }

        private static void NormalExtremeRoleAssign(
            RoleAssignmentData extremeRolesData,
            List<int> shuffledArange)
        {

            List<SingleRoleAbs> shuffleRolesForImpostor = extremeRolesData.RolesForVanillaImposter;
            List<SingleRoleAbs> shuffleRolesForCrewmate = extremeRolesData.RolesForVanillaCrewmate;

            bool assigned = false;
            int assignedPlayers = 1;

            do
            {
                var rng = new Random(
                    UnityEngine.SystemInfo.processorFrequency);
                shuffledArange = shuffledArange.OrderBy(item => rng.Next()).ToList();
                Modules.Helpers.DebugLog($"NotAssignPlayerNum:{shuffledArange.Count()}");
                assignedPlayers = 1;

                List<int> tempList = new List<int>(shuffledArange);

                foreach (int index in tempList)
                {
                    var roleShuffleGen = new Random(
                        UnityEngine.SystemInfo.processorFrequency);
                    assigned = false;

                    List<SingleRoleAbs> shuffledRoles = new List<SingleRoleAbs>();
                    PlayerControl player = PlayerControl.AllPlayerControls[index];
                    RoleBehaviour roleData = player.Data.Role;

                    // Modules.Helpers.DebugLog($"ShufflePlayerIndex:{shuffledArange.Count()}");

                    switch (roleData.Role)
                    {

                        case RoleTypes.Impostor:
                            shuffledRoles = shuffleRolesForCrewmate.OrderBy(
                                item => roleShuffleGen.Next()).ToList();
                            break;
                        case RoleTypes.Crewmate:
                            shuffledRoles = shuffleRolesForCrewmate.OrderBy(
                                item => roleShuffleGen.Next()).ToList();
                            break;
                        default:
                            SetRoleToPlayer(
                                player,
                                (byte)roleData.Role);
                            shuffledArange.Remove(index);
                            assigned = true;
                            break;
                    }
                    
                    if (assigned)
                    {
                        ++assignedPlayers;
                        continue; 
                    };

                    bool result = false;
                    foreach (var role in shuffledRoles)
                    {
                        var(roleNum, spawnRate) = extremeRolesData.RoleSpawnSettings[
                            roleData.Role][role.BytedRoleId];
                        result = IsRoleSpawn(roleNum, spawnRate);

                        Modules.Helpers.DebugLog($"RoleResult:{result}");

                        if (result)
                        {
                            SetRoleToPlayer(player, role.BytedRoleId);
                            shuffledArange.Remove(index);
                            extremeRolesData.RoleSpawnSettings[roleData.Role][role.BytedRoleId] = (
                                --roleNum,
                                spawnRate);
                            assigned = false;
                            break;
                        }
                        else
                        {
                            assigned = true;
                        }
                    }
                    if (assigned) { ++assignedPlayers; }
                    
                }

                Modules.Helpers.DebugLog($"AssignedPlayerNum:{assignedPlayers}");

                if (shuffledArange.Count == assignedPlayers ||
                    (shuffleRolesForImpostor.Count == 0 && 
                     shuffleRolesForCrewmate.Count == 0))
                {
                    tempList = new List<int>(shuffledArange);

                    foreach (int index in tempList)
                    {
                        PlayerControl player = PlayerControl.AllPlayerControls[index];
                        SetRoleToPlayer(
                            player, (byte)(player.Data.Role.Role));
                        shuffledArange.Remove(index);
                    }

                }

            }
            while (shuffledArange.Count != 0);
        }


        private static void SetRoleToPlayer(
            PlayerControl player, byte roleId, bool multiAssign=false)
        {

            Modules.Helpers.DebugLog($"Player:{player.name}  RoleId:{roleId}");

            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                PlayerControl.LocalPlayer.NetId,
                (byte)CustomRPC.SetRole, Hazel.SendOption.Reliable, -1);
            
            writer.Write(roleId);
            writer.Write(player.PlayerId);
            writer.Write(multiAssign);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            ExtremeRoleRPC.SetRole(roleId, player.PlayerId, multiAssign);
        }

        private static bool IsRoleSpawn(
            int roleNum, int spawnData)
        {
            if (roleNum <= 0) { return false; }
            if (spawnData < UnityEngine.Random.RandomRange(0, 100)) { return false; }

            return true;
        }

        private static bool IsMultiAssignRole(
            MultiAssignRoleAbs role,
            PlayerControl player)
        {
            switch (player.Data.Role.Role)
            {
                case RoleTypes.Impostor:
                    if (role.IsImposter())
                    {
                       return true;
                    }
                    break;
                case RoleTypes.Crewmate:
                    if ((role.IsCrewmate() || role.IsNeutral()) &&
                         role.Teams != ExtremeRoleType.Null)
                    {
                        return true;
                    }
                    break;
                case RoleTypes.Shapeshifter:
                    if (role.IsImposter() && role.CanHasAnotherRole)
                    {
                        return true;
                    }
                    break;
                case RoleTypes.Engineer:
                case RoleTypes.Scientist:
                    if ((role.IsCrewmate() || role.IsNeutral()) && 
                        role.CanHasAnotherRole &&
                        role.Teams != ExtremeRoleType.Null)
                    {
                        return true;
                    }
                    break;
                default:
                    return false;
            }
            return false;
        }

        private static RoleAssignmentData CreateRoleData()
        {
            List<SingleRoleAbs> RolesForVanillaImposter = new List<SingleRoleAbs>();
            List<SingleRoleAbs> RolesForVanillaCrewmate = new List<SingleRoleAbs>();
            
            // コンビネーションロールに含まれているロール、コンビネーション全体のスポーン数、スポーンレート
            List<(List<MultiAssignRoleAbs>, (int, int))> combinationRole = new List<
                (List<MultiAssignRoleAbs>, (int, int))>();

            Dictionary<byte, (int, int)> RoleSpawnSettingsForImposter = new Dictionary<byte, (int, int)>();
            Dictionary<byte, (int, int)> RoleSpawnSettingsForCrewmate = new Dictionary<byte, (int, int)>();

            var allSetting = OptionsHolder.AllOptions;

            int crewmateRolesNum = UnityEngine.Random.RandomRange(
                allSetting[(int)OptionsHolder.CommonOptionKey.MinCremateRoles].GetSelection(),
                allSetting[(int)OptionsHolder.CommonOptionKey.MaxCremateRoles].GetSelection());
            int neutralRolesNum = UnityEngine.Random.RandomRange(
                allSetting[(int)OptionsHolder.CommonOptionKey.MinNeutralRoles].GetSelection(),
                allSetting[(int)OptionsHolder.CommonOptionKey.MaxNeutralRoles].GetSelection());
            int impostorRolesNum = UnityEngine.Random.RandomRange(
                allSetting[(int)OptionsHolder.CommonOptionKey.MinNeutralRoles].GetSelection(),
                allSetting[(int)OptionsHolder.CommonOptionKey.MaxNeutralRoles].GetSelection());


            foreach (var role in ExtremeRoleManager.CombRole)
            {
                int spawnRate = allSetting[
                    role.GetRoleSettingId(RoleCommonSetting.SpawnRate)].GetPercentage();
                int roleSet = allSetting[
                    role.GetRoleSettingId(RoleCommonSetting.RoleNum)].GetSelection() + 1;

                Modules.Helpers.DebugLog($"SpawnRate:{spawnRate}   RoleSet:{roleSet}");

                if (roleSet <= 0 || spawnRate <= 0.0)
                {
                    continue;
                }

                combinationRole.Add(
                    (role.Roles, (roleSet * role.Roles.Count, spawnRate)));
            }

            foreach (var role in ExtremeRoleManager.NormalRole)
            {

                byte roleId = role.BytedRoleId;
                int spawnRate = allSetting[
                    role.GetRoleSettingId(RoleCommonSetting.SpawnRate)].GetPercentage();
                int roleNum = allSetting[
                    role.GetRoleSettingId(RoleCommonSetting.RoleNum)].GetInt();

                Modules.Helpers.DebugLog(
                    $"SelectopmValue:{allSetting[role.GetRoleSettingId(RoleCommonSetting.RoleNum)].Selections[0]}");
                Modules.Helpers.DebugLog(
                    $"Role Name:{role.RoleName}  SpawnRate:{spawnRate}   RoleNum:{roleNum}");

                if (roleNum <= 0 || spawnRate <= 0.0)
                {
                    continue;
                }

                (int, int) addData = (
                    roleNum,
                    spawnRate);

                switch (role.Teams)
                {
                    case ExtremeRoleType.Impostor:
                        RolesForVanillaImposter.Add(role);
                        RoleSpawnSettingsForImposter[roleId] = addData;
                        break;
                    case ExtremeRoleType.Crewmate:
                    case ExtremeRoleType.Neutral:
                        RolesForVanillaCrewmate.Add(role);
                        RoleSpawnSettingsForCrewmate[roleId] = addData;
                        break;
                    case ExtremeRoleType.Null:
                        break;
                }
            }


            return new RoleAssignmentData
            {
                RolesForVanillaCrewmate = RolesForVanillaCrewmate,
                RolesForVanillaImposter = RolesForVanillaImposter,
                CombinationRole = combinationRole,

                RoleSpawnSettings = new Dictionary<RoleTypes, Dictionary<byte, (int, int)>>()
                { {RoleTypes.Impostor, RoleSpawnSettingsForImposter},
                  {RoleTypes.Crewmate, RoleSpawnSettingsForCrewmate},
                },

                CrewmateRoles = crewmateRolesNum,
                NeutralRoles = neutralRolesNum,
                ImpostorRoles = impostorRolesNum,
            };
        }

        private class RoleAssignmentData
        {
            public List<SingleRoleAbs> RolesForVanillaImposter = new List<SingleRoleAbs>();
            public List<SingleRoleAbs> RolesForVanillaCrewmate = new List<SingleRoleAbs>();
            public List<(List<MultiAssignRoleAbs>, (int, int))> CombinationRole = new List<
                (List<MultiAssignRoleAbs>, (int, int))>();

            public Dictionary<
                RoleTypes, Dictionary<byte, (int, int)>> RoleSpawnSettings = 
                    new Dictionary<RoleTypes, Dictionary<byte, (int, int)>>();
            public int CrewmateRoles { get; set; }
            public int NeutralRoles { get; set; }
            public int ImpostorRoles { get; set; }
        }

    }
}
