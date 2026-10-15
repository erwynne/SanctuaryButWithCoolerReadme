using System;
using System.Linq;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Sanctuary.Core.Helpers;
using Sanctuary.Game;
using Sanctuary.Database;
using Sanctuary.Database.Entities;
using Sanctuary.Packet;
using Sanctuary.Packet.Common;
using Sanctuary.Packet.Common.Attributes;

namespace Sanctuary.Gateway.Handlers;

[PacketHandler]
public static class InventoryPacketUseImmediateActivationItemHandler
{
    private static bool TakeOne(DatabaseContext dbContext, DbItem dbItem, ClientItem clientItem)

{
    var deleteItem = clientItem.Count == 1;

    if (deleteItem)
        dbContext.Items.Remove(dbItem);
    else
        dbItem.Count -= 1;

    return deleteItem;
}
    private static ILogger _logger = null!;
    private static IResourceManager _resourceManager = null!;
    private static IDbContextFactory<DatabaseContext> _dbContextFactory = null!;
    
    private static void UpdatePlayer(GatewayConnection connection, ClientItem clientItem, bool delete)
{
        if (delete)
        {
            connection.Player.Items.Remove(clientItem);
            var clientUpdatePacketItemDelete = new ClientUpdatePacketItemDelete
            {
                ItemGuid = clientItem.Id
            };

            connection.SendTunneled(clientUpdatePacketItemDelete);
        }
        else
        {
            var clientUpdatePacketItemUpdate = new ClientUpdatePacketItemUpdate
            {
                ItemGuid = clientItem.Id,
                Count = clientItem.Count -= 1
            };

            connection.SendTunneled(clientUpdatePacketItemUpdate);
        }
}

    public static void ConfigureServices(IServiceProvider serviceProvider)
    {
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        _logger = loggerFactory.CreateLogger(nameof(InventoryPacketUseImmediateActivationItemHandler));

        _resourceManager = serviceProvider.GetRequiredService<IResourceManager>();
        _dbContextFactory = serviceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    }

    public static bool HandlePacket(GatewayConnection connection, ReadOnlySpan<byte> data)
    {
        if (!InventoryPacketUseImmediateActivationItem.TryDeserialize(data, out var packet))
        {
            _logger.LogError("Failed to deserialize {packet}.", nameof(InventoryPacketUseImmediateActivationItem));
            return false;
        }

        _logger.LogTrace("Received {name} packet. ( {packet} )", nameof(InventoryPacketUseImmediateActivationItem), packet);

        var clientItem = connection.Player.Items.SingleOrDefault(x => x.Id == packet.ItemGuid);

        if (clientItem is null)
        {
            _logger.LogWarning("Unknown item guid. {guid}", packet.ItemGuid);
            return true;
        }

            if (!_resourceManager.ClientItemDefinitions.TryGetValue(clientItem.Definition, out var clientItemDefinition))
        {
            _logger.LogWarning("Unknown item definition. {theitemdefinition}", clientItem.Definition);
            return true;
        }

        if (clientItemDefinition.Type != 28)
        {
             _logger.LogWarning("Unknown or incorrect Mystery Chest type supplied. {theitemtype}", clientItemDefinition.Type);
            return true;
        }

        var clientKey = connection.Player.Items.FirstOrDefault(x => x.Definition == 78137);

        if (clientKey is null)
        {
            _logger.LogWarning("Client did not have a Mystery Key!");
            return true;
        }
        
         using var dbContext = _dbContextFactory.CreateDbContext();

        var dbCharacter = dbContext.Characters.FirstOrDefault(x => x.Id == GuidHelper.GetPlayerId(connection.Player.Guid));

        if (dbCharacter is null)
        {
            _logger.LogWarning("Invalid database character.");
            return true;
        }

        var dbChest = dbContext.Items.SingleOrDefault(x => x.CharacterId == dbCharacter.Id && x.Id == clientItem.Id);
        var dbKey = dbContext.Items.SingleOrDefault(x => x.CharacterId == dbCharacter.Id && x.Id == clientKey.Id);

        if (dbChest is null)
        {
            _logger.LogWarning("Invalid database item/chest.");
            return true;
        }
        if (dbKey is null)
        {
            _logger.LogWarning("Invalid database item/key.");
            return true;
        }

        var deleteChest = TakeOne(dbContext, dbChest, clientItem);
        var deleteKey = TakeOne(dbContext, dbKey, clientKey);

        if (dbContext.SaveChanges() <= 0)
        {
            _logger.LogWarning("Failed to save to database.");
            return true;
        }

        UpdatePlayer(connection, clientItem, deleteChest);
        UpdatePlayer(connection, clientKey, deleteKey);

        _logger.LogInformation("Item being opened. GUID is {guid} and item definition is {theitemdefinition}, key is {thekey}", packet.ItemGuid, clientItem.Definition, clientKey.Id);

        return true;
    }
}