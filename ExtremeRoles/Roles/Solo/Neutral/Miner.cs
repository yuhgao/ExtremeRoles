﻿using System.Collections.Generic;

using Hazel;
using UnityEngine;

using ExtremeRoles.Module;
using ExtremeRoles.Module.ExtremeShipStatus;
using ExtremeRoles.Roles.API;
using ExtremeRoles.Roles.API.Interface;
using ExtremeRoles.Roles.API.Extension.Neutral;
using ExtremeRoles.Performance;
using ExtremeRoles.Performance.Il2Cpp;
using ExtremeRoles.Resources;
using ExtremeRoles.Module.CustomMonoBehaviour;

namespace ExtremeRoles.Roles.Solo.Neutral;

public sealed class Miner : SingleRoleBase, IRoleAbility, IRoleUpdate, IRoleSpecialReset
{
    public enum MinerOption
    {
        MineKillRange,
        NoneActiveTime,
        ShowKillLog
    }
	public enum MinerRpc
	{
		SetMine,
		ActiveMine,
		RemoveMine,
	}

    public ExtremeAbilityButton Button { get; set; }

    private List<MinerMineEffect> mines;
    private float killRange;
    private float nonActiveTime;
    private float timer;
    private bool isShowKillLog;
	private MinerMineEffect noneActiveMine = null;
    private TextPopUpper killLogger = null;

    public Miner() : base(
        ExtremeRoleId.Miner,
        ExtremeRoleType.Neutral,
        ExtremeRoleId.Miner.ToString(),
        ColorPalette.MinerIvyGreen,
        false, false, true, false)
    { }

	public static void RpcHandle(ref MessageReader reader)
	{
		byte playerId = reader.ReadByte();
		Miner miner = ExtremeRoleManager.GetSafeCastedRole<Miner>(playerId);
		MinerRpc rpc = (MinerRpc)reader.ReadByte();
		switch (rpc)
		{
			case MinerRpc.SetMine:
				float x = reader.ReadSingle();
				float y = reader.ReadSingle();
				if (miner == null) { return; }
				setMine(miner, new(x, y));
				break;
			case MinerRpc.ActiveMine:
				if (miner == null) { return; }
				activateMine(miner);
				break;
			case MinerRpc.RemoveMine:
				if (miner == null) { return; }
				int index =reader.ReadInt32();
				removeMine(miner, index);
				break;
		}
	}

	private static void setMine(Miner miner, Vector2 pos)
	{
		GameObject obj = new GameObject($"Miner{miner.GameControlId}_Mine{miner.mines.Count}");
		obj.transform.position = pos;
		var mine = obj.AddComponent<MinerMineEffect>();
		miner.noneActiveMine = mine;
		miner.noneActiveMine.SetParameter(miner.killRange);
		ExtremeRolesPlugin.ShipState.AddMeetingResetObject(miner.noneActiveMine);
	}

	private static void activateMine(Miner miner)
	{
		if (miner.noneActiveMine == null) { return; }
		miner.noneActiveMine.SwithAcitve();
		miner.mines.Add(miner.noneActiveMine);
	}

	private static void removeMine(Miner miner, int index)
	{
		miner.mines[index].Clear();
		miner.mines.RemoveAt(index);
	}

	public void CreateAbility()
    {
        this.CreateNormalAbilityButton(
            "setMine",
            Loader.CreateSpriteFromResources(
                Path.MinerSetMine),
            abilityOff: CleanUp,
            forceAbilityOff: () => { });
    }

    public bool UseAbility()
    {
		setMine(this, CachedPlayerControl.LocalPlayer.PlayerControl.GetTruePosition());
		return true;
    }

    public void CleanUp()
    {
		activateMine(this);
	}

    public bool IsAbilityUse() => this.IsCommonUse();

    public void AllReset(PlayerControl rolePlayer)
    {
        this.mines.Clear();
    }

    public void ResetOnMeetingStart()
    {
        if (this.killLogger != null)
        {
            this.killLogger.Clear();
        }
    }

    public void ResetOnMeetingEnd(GameData.PlayerInfo exiledPlayer = null)
    {
        return;
    }

    public void Update(PlayerControl rolePlayer)
    {
        if (rolePlayer.Data.IsDead ||
			rolePlayer.Data.Disconnected ||
			CachedShipStatus.Instance == null ||
			GameData.Instance == null ||
			!CachedShipStatus.Instance.enabled ||
			ExtremeRolesPlugin.ShipState.AssassinMeetingTrigger) { return; }

        if (MeetingHud.Instance || ExileController.Instance)
        {
            this.timer = this.nonActiveTime;
            return;
        }

        if (this.timer > 0.0f)
        {
            this.timer -= Time.deltaTime;
            return;
        }

        if (this.mines.Count == 0) { return; }

        HashSet<int> activateMine = new HashSet<int>();
        HashSet<byte> killedPlayer = new HashSet<byte>();

        for (int i = 0; i < this.mines.Count; ++i)
        {
            Vector2 pos = this.mines[i].transform.position;

            foreach (GameData.PlayerInfo playerInfo in
                GameData.Instance.AllPlayers.GetFastEnumerator())
            {
                if (playerInfo == null ||
					killedPlayer.Contains(playerInfo.PlayerId)) { continue; }

                var assassin = ExtremeRoleManager.GameRole[
                    playerInfo.PlayerId] as Combination.Assassin;

                if (assassin != null &&
					(!assassin.CanKilled || !assassin.CanKilledFromNeutral))
                {
					continue;
				}

                if (!playerInfo.Disconnected &&
                    !playerInfo.IsDead &&
                    playerInfo.Object != null &&
                    !playerInfo.Object.inVent)
                {
					Vector2 vector = playerInfo.Object.GetTruePosition() - pos;
					float magnitude = vector.magnitude;
					if (magnitude <= this.killRange &&
						!PhysicsHelpers.AnyNonTriggersBetween(
							pos, vector.normalized,
							magnitude, Constants.ShipAndObjectsMask))
					{
						activateMine.Add(i);
						killedPlayer.Add(playerInfo.PlayerId);
						break;
					}
				}
            }
        }

        foreach (int index in activateMine)
        {
			removeMine(this, index);
        }

        foreach (byte player in killedPlayer)
        {
            Helper.Player.RpcUncheckMurderPlayer(
                rolePlayer.PlayerId,
                player, 0);
            ExtremeRolesPlugin.ShipState.RpcReplaceDeadReason(
                player, ExtremeShipStatus.PlayerStatus.Explosion);

            if (this.isShowKillLog)
            {
                GameData.PlayerInfo killPlayer = GameData.Instance.GetPlayerById(player);

                if (killPlayer != null)
                {
                    // 以下のテキスト表示処理
                    // [AUER32-ACM] {プレイヤー名} 100↑
                    // AmongUs ExtremeRoles v3.2.0.0 - AntiCrewmateMine
                    this.killLogger.AddText(
                        $"[AUER32-ACM] {Helper.Design.ColoedString(new Color32(255, 153, 51, byte.MaxValue), killPlayer.DefaultOutfit.PlayerName)} 100↑");
                }
            }
        }

    }

    public override void ExiledAction(PlayerControl rolePlayer)
    {
        this.mines.Clear();
    }
    public override void RolePlayerKilledAction(
        PlayerControl rolePlayer, PlayerControl killerPlayer)
    {
        this.mines.Clear();
    }

    public override bool IsSameTeam(SingleRoleBase targetRole) =>
        this.IsNeutralSameTeam(targetRole);

    protected override void CreateSpecificOption(
        IOptionInfo parentOps)
    {
        this.CreateCommonAbilityOption(
            parentOps, 2.0f);
        CreateFloatOption(
            MinerOption.MineKillRange,
            1.8f, 0.5f, 5f, 0.1f, parentOps);
        CreateFloatOption(
            MinerOption.NoneActiveTime,
            20.0f, 1.0f, 45f, 0.5f,
            parentOps, format: OptionUnit.Second);
        CreateBoolOption(
            MinerOption.ShowKillLog,
            true, parentOps);
    }

    protected override void RoleSpecificInit()
    {
        var allOpt = OptionManager.Instance;

        this.killRange = allOpt.GetValue<float>(
            GetRoleOptionId(MinerOption.MineKillRange));
        this.nonActiveTime = allOpt.GetValue<float>(
            GetRoleOptionId(MinerOption.NoneActiveTime));
        this.isShowKillLog = allOpt.GetValue<bool>(
            GetRoleOptionId(MinerOption.ShowKillLog));

        this.mines = new List<MinerMineEffect>();
        this.timer = this.nonActiveTime;

		this.killLogger = new TextPopUpper(
            2, 3.5f, new Vector3(0, -1.2f, 0.0f),
            TMPro.TextAlignmentOptions.Center, false);
    }
}
