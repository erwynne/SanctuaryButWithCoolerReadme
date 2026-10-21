using Sanctuary.Game.Entities;
using Sanctuary.Game.Helpers;
using Sanctuary.Game.Resources.Definitions.Rewards;

namespace Sanctuary.Game.ChatCommands;

/// <summary>
/// Uses rewards defined in Rewards.json
/// </summary>
public class RewardChatCommand : IChatCommand
{
    private readonly IChatCommandManager _chatCommandManager;
    private readonly IRewardManager _rewardManager;

    public string KeyWord => "reward";
    public string Usage => "<tableKey>";
    public string Description => "Rolls and grants a reward from the given reward table, for testing.";
    public ChatCommandRole RequiredRole => ChatCommandRole.Admin;

    public RewardChatCommand(IChatCommandManager chatCommandManager, IRewardManager rewardManager)
    {
        _chatCommandManager = chatCommandManager;
        _rewardManager = rewardManager;
    }

    public bool Handle(Player invoker, string[] args)
    {
        if (args.Length != 1)
            return false;

        var tableKey = args[0];

        if (!_rewardManager.TryRollRewardUnowned(invoker, tableKey, out var drop, out var tint) || drop is null)
        {
            ChatHelper.SendSystemMessage(invoker, $"Nothing left to win in '{tableKey}'.");
            return true;
        }

        var granted = drop is ItemRewardDropDefinition item
            ? _rewardManager.TryGrantItem(invoker, item.ItemDefinitionId, tint, item.Quantity)
            : _rewardManager.TryGrantReward(invoker, drop);

        if (!granted)
        {
            ChatHelper.SendSystemMessage(invoker, "Failed to grant the rolled reward.");
            return true;
        }

        _chatCommandManager.LogAction(this, invoker, "Reward test grant", null, $"table={tableKey}");
        ChatHelper.SendSystemMessage(invoker, $"Rolled and granted a reward from '{tableKey}'.");

        return true;
    }
}
