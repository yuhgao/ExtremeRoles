﻿using ExtremeRoles.GhostRoles.API;
using ExtremeRoles.Module;
using ExtremeRoles.Module.AbilityButton.GhostRoles;
using ExtremeRoles.Roles;
using ExtremeRoles.Roles.API;
using ExtremeRoles.Performance;
using Hazel;
using System.Collections.Generic;
using UnityEngine;

using ExtremeRoles.Helper;

namespace ExtremeRoles.GhostRoles.Crewmate
{
    public class Poltergeist : GhostRoleBase
    {
        public enum Option
        {
            Range,
        }

        public DeadBody CarringBody;

        private float range;
        private GameData.PlayerInfo targetBody;

        public Poltergeist() : base(
            true,
            ExtremeRoleType.Crewmate,
            ExtremeGhostRoleId.Poltergeist,
            ExtremeGhostRoleId.Poltergeist.ToString(),
            ColorPalette.PoltergeistKenpou)
        { }

        public static void DeadbodyMove(
            byte playerId, byte targetPlayerId, bool pickUp)
        {

            var rolePlayer = Player.GetPlayerControlById(playerId);
            var role = ExtremeGhostRoleManager.GetSafeCastedGhostRole<Poltergeist>(playerId);
            if (role == null) { return; }

            if (pickUp)
            {
                pickUpDeadBody(rolePlayer, role, targetPlayerId);
            }
            else
            {
                setDeadBody(rolePlayer, role);
            }
        }
        private static void pickUpDeadBody(
            PlayerControl rolePlayer,
            Poltergeist role,
            byte targetPlayerId)
        {

            DeadBody[] array = UnityEngine.Object.FindObjectsOfType<DeadBody>();
            for (int i = 0; i < array.Length; ++i)
            {
                if (GameData.Instance.GetPlayerById(array[i].ParentId).PlayerId == targetPlayerId)
                {
                    role.CarringBody = array[i];
                    role.CarringBody.transform.position = rolePlayer.transform.position;
                    role.CarringBody.transform.SetParent(rolePlayer.transform);
                    break;
                }
            }
        }

        private static void setDeadBody(
            PlayerControl rolePlayer,
            Poltergeist role)
        {
            if (role.CarringBody == null) { return; }
            if (role.CarringBody.transform.parent != rolePlayer.transform) { return; }

            role.CarringBody.transform.parent = null;
            role.CarringBody.transform.position = rolePlayer.GetTruePosition() + new Vector2(0.15f, 0.15f);
            role.CarringBody.transform.position -= new Vector3(0.0f, 0.0f, 0.01f);
            role.CarringBody = null;
        }

        public override void CreateAbility()
        {
            this.Button = new ReusableAbilityButton(
                GhostRoleAbilityManager.AbilityType.PoltergeistMoveDeadbody,
                this.UseAbility,
                this.isPreCheck,
                this.isAbilityUse,
                Resources.Loader.CreateSpriteFromResources(
                    Resources.Path.CarrierCarry),
                this.DefaultButtonOffset,
                rpcHostCallAbility: abilityCall,
                abilityCleanUp: cleanUp);
            this.ButtonInit();
            this.Button.SetLabelToCrewmate();
        }

        public override HashSet<ExtremeRoleId> GetRoleFilter() => new HashSet<ExtremeRoleId>();

        public override void Initialize()
        {
            this.range = OptionHolder.AllOption[
                GetRoleOptionId(Option.Range)].GetValue();
        }

        public override void ReseOnMeetingEnd()
        {
            return;
        }

        public override void ReseOnMeetingStart()
        {
            this.targetBody = null;
        }

        protected override void CreateSpecificOption(
            CustomOptionBase parentOps)
        {
            CreateFloatOption(
                Option.Range, 1.0f,
                0.2f, 3.0f, 0.1f,
                parentOps);
            CreateButtonOption(
                parentOps, 3.0f);
        }

        protected override void UseAbility(MessageWriter writer)
        {
            writer.Write(PlayerControl.LocalPlayer.PlayerId);
            writer.Write(this.targetBody.PlayerId);
            writer.Write(true);
        }

        private bool isPreCheck() => this.targetBody != null;

        private bool isAbilityUse()
        {
            this.targetBody = null;

            if (CachedShipStatus.Instance == null ||
                !CachedShipStatus.Instance.enabled) { return false; }

            foreach (Collider2D collider2D in Physics2D.OverlapCircleAll(
                CachedPlayerControl.LocalPlayer.PlayerControl.GetTruePosition(),
                this.range,
                Constants.PlayersOnlyMask))
            {
                if (collider2D.tag == "DeadBody")
                {
                    DeadBody component = collider2D.GetComponent<DeadBody>();

                    if (component && !component.Reported && component.transform.parent == null)
                    {
                        Vector2 truePosition = CachedPlayerControl.LocalPlayer.PlayerControl.GetTruePosition();
                        Vector2 truePosition2 = component.TruePosition;
                        if ((Vector2.Distance(truePosition2, truePosition) <= range) &&
                            (PlayerControl.LocalPlayer.CanMove) &&
                            (!PhysicsHelpers.AnythingBetween(
                                truePosition, truePosition2,
                                Constants.ShipAndObjectsMask, false)))
                        {
                            this.targetBody = GameData.Instance.GetPlayerById(component.ParentId);
                            break;
                        }
                    }
                }
            }

            return this.IsCommonUse() && this.targetBody != null;
        }
        private void abilityCall()
        {
            pickUpDeadBody(CachedPlayerControl.LocalPlayer, this, this.targetBody.PlayerId);
            this.targetBody = null;
        }
        private void cleanUp()
        {
            MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(
                    PlayerControl.LocalPlayer.NetId,
                    (byte)RPCOperator.Command.UseGhostRoleAbility,
                    Hazel.SendOption.Reliable, -1);
            writer.Write((byte)GhostRoleAbilityManager.AbilityType.PoltergeistMoveDeadbody);
            writer.Write(PlayerControl.LocalPlayer.PlayerId);
            writer.Write(byte.MinValue);
            writer.Write(false);
            AmongUsClient.Instance.FinishRpcImmediately(writer);
            setDeadBody(CachedPlayerControl.LocalPlayer, this);
        }
    }
}