﻿using UnityEngine;

using ExtremeRoles.Helper;
using ExtremeRoles.Module;
using ExtremeRoles.Resources;
using ExtremeRoles.Roles.API;
using ExtremeRoles.Roles.API.Interface;
using ExtremeRoles.Performance;
using ExtremeRoles.Module.SystemType;
using ExtremeRoles.Module.SystemType.Roles;

namespace ExtremeRoles.Roles.Solo.Impostor;

public sealed class Faker : SingleRoleBase, IRoleAbility
{
	public enum FakerDummyOps : byte
	{
		DeadBody,
		Player,
	}

	public ExtremeAbilityButton Button
	{
		get => this.createFake;
		set
		{
			this.createFake = value;
		}
	}

	private ExtremeAbilityButton createFake;

	private Sprite deadBodyDummy;
	private Sprite playerDummy;

	private string deadBodyDummyStr;
	private string playerDummyStr;

	public Faker() : base(
		ExtremeRoleId.Faker,
		ExtremeRoleType.Impostor,
		ExtremeRoleId.Faker.ToString(),
		Palette.ImpostorRed,
		true, false, true, true)
	{ }

	public void CreateAbility()
	{
		this.deadBodyDummy = Loader.CreateSpriteFromResources(
			Path.FakerDummyDeadBody, 115f);
		this.playerDummy = Loader.CreateSpriteFromResources(
			Path.FakerDummyPlayer, 115f);

		this.deadBodyDummyStr = Translation.GetString("dummyDeadBody");
		this.playerDummyStr = Translation.GetString("dummyPlayer");

		this.CreateNormalAbilityButton(
			"dummyDeadBody",
			this.deadBodyDummy);
	}

    public bool IsAbilityUse()
    {
        bool isPlayerDummy = Key.IsShift();

		this.Button.Behavior.SetGraphic(
			isPlayerDummy ? this.playerDummyStr : this.deadBodyDummyStr,
			isPlayerDummy ? this.playerDummy : this.deadBodyDummy);

		return this.IsCommonUse();
	}

	public void ResetOnMeetingEnd(GameData.PlayerInfo exiledPlayer = null)
	{
		return;
	}

	public void ResetOnMeetingStart()
	{
		return;
	}

	public bool UseAbility()
	{

		var allPlayer = GameData.Instance.AllPlayers;

        bool isPlayerMode = Key.IsShift();
		bool excludeImp = Key.IsControlDown();
		bool excludeMe = Key.IsAltDown();

		byte localPlayerId = CachedPlayerControl.LocalPlayer.PlayerId;

		bool contine;
		byte targetPlayerId;

		do
		{
			int index = Random.RandomRange(0, allPlayer.Count);
			var player = allPlayer[index];
			targetPlayerId = player.PlayerId;

			contine = player.IsDead || player.Disconnected;
			if (!contine && excludeImp)
			{
				contine = ExtremeRoleManager.GameRole[targetPlayerId].IsImpostor();
			}
			else if (!contine && excludeMe)
			{
				contine = localPlayerId == targetPlayerId;
			}

		} while (contine);

		byte ops = isPlayerMode ? (byte)FakerDummyOps.Player : (byte)FakerDummyOps.DeadBody;

		ExtremeSystemTypeManager.RpcUpdateSystem(
			ExtremeSystemType.FakerDummy, (writer) =>
			{
				writer.Write(localPlayerId);
				writer.Write(targetPlayerId);
				writer.Write(ops);
			});

		return true;
	}

	protected override void CreateSpecificOption(
		IOptionInfo parentOps)
	{
		this.CreateCommonAbilityOption(
			parentOps);
	}

	protected override void RoleSpecificInit()
	{
		this.RoleAbilityInit();
		ExtremeSystemTypeManager.Instance.TryAdd(ExtremeSystemType.FakerDummy, new FakerDummySystem());
	}
}
