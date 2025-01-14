﻿using System.Text;

using HarmonyLib;

using ExtremeRoles.Extension.Il2Cpp;
using ExtremeRoles.Module.SystemType;
using ExtremeRoles.GhostRoles;
using ExtremeRoles.Roles;
using ExtremeRoles.Roles.API;
using ExtremeRoles.Roles.API.Interface;

using ExtremeRoles.Performance;

using PlayerHeler = ExtremeRoles.Helper.Player;

namespace ExtremeRoles.Patches.Meeting.Hud;

#nullable enable

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Start))]
public static class MeetingHudStartPatch
{
	public static void Postfix(MeetingHud __instance)
	{

		var state = ExtremeRolesPlugin.ShipState;
		bool trigger = state.AssassinMeetingTrigger;
		var builder = new StringBuilder();

		builder
			.AppendLine("------ MeetingHud Start!! -----")
			.AppendLine(" - Meeting info:");

		if (GameManager.Instance.LogicOptions.IsTryCast<LogicOptionsNormal>(out var opt))
		{
			builder
				.Append("   - Discussion Time:")
				.Append(opt!.GetDiscussionTime())
				.AppendLine()

				.Append("   - Voting Time:")
				.Append(opt!.GetVotingTime())
				.AppendLine();
		}

		builder
			.Append("   - Assassin Meeting:")
			.Append(trigger)
			.AppendLine();

		if (!trigger &&
			ExtremeSystemTypeManager.Instance.TryGet<MeetingTimeChangeSystem>(
				ExtremeSystemType.MeetingTimeOffset, out var system) &&
			system is not null)
		{

			__instance.discussionTimer -= system.HudTimerStartOffset;

			builder
				.AppendLine("   - TimeOffset System: Enable")
				.Append("     - DiscussionTimer start at:").Append(__instance.discussionTimer);

		}
		else
		{
			builder.Append("   - TimeOffset System: Disable");
		}

		var logger = ExtremeRolesPlugin.Logger;

		logger.LogInfo(builder.ToString());

		logger.LogInfo(" --- Start Meeting Start Reseting --- ");

		logger.LogInfo("Resetting Start: ShipStatus Systems");
		ExtremeRolesPlugin.ShipState.ClearMeetingResetObject();
		ExtremeSystemTypeManager.Instance.Reset(null, (byte)ResetTiming.MeetingStart);
		logger.LogInfo("Resetting End: ShipStatus Systems");

		logger.LogInfo("Resetting Start: PlayerControl");
		PlayerHeler.ResetTarget();
		logger.LogInfo("Resetting End: PlayerControl");

		logger.LogInfo("Resetting Start: Modding MeetingHud system");
		MeetingHudSelectPatch.SetSelectBlock(false);
		logger.LogInfo("Resetting End: Modding MeetingHud system");

		if (ExtremeRoleManager.GameRole.Count == 0) { return; }

		logger.LogInfo("Resetting Start: ExR Normal and Combination Roles");
		var role = ExtremeRoleManager.GetLocalPlayerRole();

		if (role is IRoleAbility abilityRole)
		{
			abilityRole.Button.OnMeetingStart();
		}
		if (role is IRoleResetMeeting resetRole)
		{
			resetRole.ResetOnMeetingStart();
		}
		if (role is MultiAssignRoleBase multiAssignRole)
		{
			if (multiAssignRole.AnotherRole is IRoleAbility multiAssignAbilityRole)
			{
				multiAssignAbilityRole.Button.OnMeetingStart();
			}
			if (multiAssignRole.AnotherRole is IRoleResetMeeting multiAssignResetRole)
			{
				multiAssignResetRole.ResetOnMeetingStart();
			}
		}
		logger.LogInfo("Resetting End: ExR Normal and Combination Roles");

		logger.LogInfo("Resetting Start: ExR Ghost Roles");
		var ghostRole = ExtremeGhostRoleManager.GetLocalPlayerGhostRole();
		if (ghostRole != null)
		{
			ghostRole.ResetOnMeetingStart();
		}
		logger.LogInfo("Resetting End: ExR Ghost Roles");

		if (!trigger) { return; }

		FastDestroyableSingleton<HudManager>.Instance.Chat.gameObject.SetActive(false);
	}
}
