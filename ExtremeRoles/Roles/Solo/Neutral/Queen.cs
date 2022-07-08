﻿using System;
using System.Collections.Generic;

using UnityEngine;

using ExtremeRoles.Module;
using ExtremeRoles.Module.AbilityButton.Roles;
using ExtremeRoles.Helper;
using ExtremeRoles.Resources;
using ExtremeRoles.Roles.API;
using ExtremeRoles.Roles.API.Interface;
using ExtremeRoles.Performance;

namespace ExtremeRoles.Roles.Solo.Neutral
{
    public class Queen : SingleRoleBase, IRoleAbility, IRoleSpecialReset
    {
        public enum QueenOption
        {
            Range,
            CanUseVent,
            ServantSelfKillCool
        }

        public List<byte> ServantPlayerId = new List<byte>();

        public RoleAbilityButtonBase Button
        {
            get => this.createServant;
            set
            {
                this.createServant = value;
            }
        }

        public PlayerControl Target;
        public float ServantSelfKillCool;
        private RoleAbilityButtonBase createServant;
        private float range;

        public Queen() : base(
            ExtremeRoleId.Queen,
            ExtremeRoleType.Neutral,
            ExtremeRoleId.Queen.ToString(),
            ColorPalette.QueenWhite,
            true, false, false, false)
        { }

        public static void TargetToServant(
            byte rolePlayerId, byte targetPlayerId)
        {

            Queen queen = ExtremeRoleManager.GetSafeCastedRole<Queen>(rolePlayerId);

            if (queen == null) { return; }

            var targetPlayer = Player.GetPlayerControlById(targetPlayerId);
            var targetRole = ExtremeRoleManager.GameRole[targetPlayerId];

            resetTargetAnotherRole(targetRole, targetPlayerId, targetPlayer);
            replaceVanilaRole(targetRole, targetPlayer);

            Servant servant = new Servant(
                queen, targetRole);

            if (CachedPlayerControl.LocalPlayer.PlayerId == targetPlayerId)
            {
                servant.SelfKillAbility(queen.ServantSelfKillCool);
                if (targetRole is IRoleAbility &&
                    targetRole.Team != ExtremeRoleType.Neutral)
                {
                    servant.Button.PositionOffset = new Vector3(0, 2.6f, 0);
                    servant.Button.ReplaceHotKey(KeyCode.C);
                }
            }

            if (targetRole.Team != ExtremeRoleType.Neutral)
            {
                var multiAssignRole = targetRole as MultiAssignRoleBase;
                if (multiAssignRole != null)
                {
                    multiAssignRole.Team = ExtremeRoleType.Neutral;
                    multiAssignRole.AnotherRole = null;
                    multiAssignRole.SetAnotherRole(servant);
                    setNewRole(multiAssignRole, targetPlayerId);
                }
                else
                {
                    targetRole.Team = ExtremeRoleType.Neutral;
                    servant.SetAnotherRole(targetRole);
                    setNewRole(servant, targetPlayerId);
                }
            }
            else
            {
                setNewRole(servant, targetPlayerId);
            }
            queen.ServantPlayerId.Add(targetPlayerId);
        }

        private static void resetTargetAnotherRole(
            SingleRoleBase targetRole,
            byte targetPlayerId,
            PlayerControl targetPlayer)
        {
            var multiAssignRole = targetRole as MultiAssignRoleBase;
            if (multiAssignRole != null)
            {
                if (CachedPlayerControl.LocalPlayer.PlayerId == targetPlayerId)
                {
                    IRoleResetMeeting meetingResetRole = multiAssignRole.AnotherRole as IRoleResetMeeting;
                    if (meetingResetRole != null)
                    {
                        meetingResetRole.ResetOnMeetingStart();
                    }

                    IRoleAbility abilityRole = multiAssignRole.AnotherRole as IRoleAbility;
                    if (abilityRole != null)
                    {
                        abilityRole.ResetOnMeetingStart();
                    }
                }

                IRoleSpecialReset specialResetRole = multiAssignRole.AnotherRole as IRoleSpecialReset;
                if (specialResetRole != null)
                {
                    specialResetRole.AllReset(targetPlayer);
                }
            }
        }
        private static void replaceVanilaRole(
            SingleRoleBase targetRole,
            PlayerControl targetPlayer)
        {
            var multiAssignRole = targetRole as MultiAssignRoleBase;
            if (multiAssignRole != null)
            {
                if (multiAssignRole.AnotherRole is VanillaRoleWrapper)
                {
                    FastDestroyableSingleton<RoleManager>.Instance.SetRole(
                        targetPlayer, RoleTypes.Crewmate);
                    return;
                }
            }
            
            switch (targetPlayer.Data.Role.Role)
            {
                case RoleTypes.Crewmate:
                case RoleTypes.Impostor:
                    FastDestroyableSingleton<RoleManager>.Instance.SetRole(
                        targetPlayer, RoleTypes.Crewmate);
                    break;
                default:
                    break;
            }
        }

        private static void setNewRole(
            SingleRoleBase role,
            byte targetPlayerId)
        {
            lock (ExtremeRoleManager.GameRole)
            {
                ExtremeRoleManager.GameRole[targetPlayerId] = role;
            }
        }

        public void AllReset(PlayerControl rolePlayer)
        {
            foreach (var playerId in this.ServantPlayerId)
            {
                RPCOperator.UncheckedMurderPlayer(
                    playerId, playerId,
                    byte.MaxValue);
            }
        }

        public void CreateAbility()
        {
            this.CreateAbilityCountButton(
                Translation.GetString("queenCharm"),
                Loader.CreateSpriteFromResources(
                    Path.TestButton));
        }

        public bool UseAbility()
        {
            byte targetPlayerId = this.Target.PlayerId;

            PlayerControl rolePlayer = CachedPlayerControl.LocalPlayer;

            RPCOperator.Call(
                rolePlayer.NetId,
                RPCOperator.Command.ReplaceRole,
                new List<byte>
                {
                    rolePlayer.PlayerId,
                    this.Target.PlayerId,
                    (byte)ExtremeRoleManager.ReplaceOperation.ForceReplaceToSidekick
                });
            TargetToServant(rolePlayer.PlayerId, targetPlayerId);
            return true;
        }

        public bool IsAbilityUse()
        {
            this.Target = Player.GetPlayerTarget(
                CachedPlayerControl.LocalPlayer,
                this, this.range);

            return this.Target != null && this.IsCommonUse();
        }

        public void RoleAbilityResetOnMeetingStart()
        {
            return;
        }

        public void RoleAbilityResetOnMeetingEnd()
        {
            return;
        }
        public override void ExiledAction(GameData.PlayerInfo rolePlayer)
        {
            foreach (var playerId in this.ServantPlayerId)
            {
                Player.GetPlayerControlById(playerId)?.Exiled();
            }
        }

        public override void RolePlayerKilledAction(
            PlayerControl rolePlayer, PlayerControl killerPlayer)
        {
            foreach (var playerId in this.ServantPlayerId)
            {
                RPCOperator.UncheckedMurderPlayer(
                    playerId, playerId,
                    byte.MaxValue);
            }
        }

        public override bool IsSameTeam(SingleRoleBase targetRole)
        {
            var multiAssignRole = targetRole as MultiAssignRoleBase;

            if (multiAssignRole != null)
            {
                if (multiAssignRole.AnotherRole != null)
                {
                    return this.IsSameTeam(multiAssignRole.AnotherRole);
                }
            }
            if (OptionHolder.Ship.IsSameNeutralSameWin)
            {
                return this.isSameQueenTeam(targetRole);
            }
            else
            {
                return this.isSameQueenTeam(targetRole) && this.IsSameControlId(targetRole);
            }
        }

        protected override void CreateSpecificOption(CustomOptionBase parentOps)
        {
            CreateBoolOption(
                QueenOption.CanUseVent,
                false, parentOps);

            this.CreateAbilityCountOption(
                parentOps, 1, 3);
            
            CreateFloatOption(
                QueenOption.Range,
                1.0f, 0.5f, 2.6f, 0.1f,
                parentOps);
            CreateFloatOption(
                QueenOption.ServantSelfKillCool,
                30.0f, 0.5f, 60.0f, 0.5f,
                parentOps);
        }

        protected override void RoleSpecificInit()
        {
            this.range = OptionHolder.AllOption[
                GetRoleOptionId(QueenOption.Range)].GetValue();
            this.UseVent = OptionHolder.AllOption[
                GetRoleOptionId(QueenOption.CanUseVent)].GetValue();
            this.ServantSelfKillCool = OptionHolder.AllOption[
                GetRoleOptionId(QueenOption.ServantSelfKillCool)].GetValue();
        }

        private bool isSameQueenTeam(SingleRoleBase targetRole)
        {
            return ((targetRole.Id == this.Id) || (targetRole.Id == ExtremeRoleId.Servant));
        }
    }

    public class Servant : MultiAssignRoleBase, IRoleAbility
    {
        public Servant(
            Queen queen,
            SingleRoleBase baseRole) : 
            base(
                ExtremeRoleId.Servant,
                ExtremeRoleType.Neutral,
                ExtremeRoleId.Servant.ToString(),
                ColorPalette.QueenWhite,
                baseRole.CanKill,
                baseRole.Team == ExtremeRoleType.Crewmate ? true : baseRole.HasTask,
                baseRole.UseVent,
                baseRole.UseSabotage)
        {
            this.GameControlId = queen.GameControlId;
        }

        public RoleAbilityButtonBase Button
        { 
            get => this.selfKillButton;
            set
            {
                this.selfKillButton = value;
            }
        }

        private RoleAbilityButtonBase selfKillButton;

        public void SelfKillAbility(float coolTime)
        {
            this.Button = new ReusableAbilityButton(
                Translation.GetString("selfKill"),
                this.UseAbility,
                this.IsAbilityUse,
                Loader.CreateSpriteFromResources(
                    Path.TestButton),
                new Vector3(-1.8f, -0.06f, 0),
                null, null,
                KeyCode.F, false);
            this.Button.SetAbilityCoolTime(coolTime);
        }

        public void CreateAbility()
        {
            throw new Exception("Don't call this class method!!");
        }

        public bool IsAbilityUse() => this.IsCommonUse();

        public void RoleAbilityResetOnMeetingEnd()
        {
            return;
        }

        public void RoleAbilityResetOnMeetingStart()
        {
            return;
        }

        public bool UseAbility()
        {

            byte playerId = CachedPlayerControl.LocalPlayer.PlayerId;

            RPCOperator.Call(
                CachedPlayerControl.LocalPlayer.PlayerControl.NetId,
                RPCOperator.Command.UncheckedMurderPlayer,
                new List<byte> { playerId, playerId, byte.MaxValue });
            RPCOperator.UncheckedMurderPlayer(
                playerId,
                playerId,
                byte.MaxValue);
            return true;
        }

        protected override void CreateSpecificOption(
            CustomOptionBase parentOps)
        {
            throw new Exception("Don't call this class method!!");
        }

        protected override void RoleSpecificInit()
        {
            throw new Exception("Don't call this class method!!");
        }
    }

}
