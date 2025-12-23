using System;
using System.Linq;

namespace Sandbox.UI;

public enum WorldContainerKind
{
	None = 0,
	Furnace = 1,
	StorageBox = 2
}

/// <summary>
/// Tracks which world container UI is currently open on this client.
/// Used by inventory UI for Shift+RMB quick-transfer.
/// </summary>
public static class WorldContainerUiContext
{
	public static WorldContainerKind ActiveKind { get; private set; } = WorldContainerKind.None;
	public static Guid ActiveId { get; private set; } = Guid.Empty;

	public static bool IsActive => ActiveKind != WorldContainerKind.None && ActiveId != Guid.Empty;

	public static void SetActive( WorldContainerKind kind, Guid id )
	{
		ActiveKind = kind;
		ActiveId = id;
	}

	public static void Clear()
	{
		ActiveKind = WorldContainerKind.None;
		ActiveId = Guid.Empty;
	}

	public static bool TryDepositFromPlayerInventory( VeggaInventory inv, int fromSlot, int count )
	{
		if ( inv == null || !inv.IsValid() ) return false;
		if ( !IsActive ) return false;
		if ( fromSlot < 0 || fromSlot >= inv.TotalSlots ) return false;
		if ( count <= 0 ) return false;

		var scene = Game.ActiveScene;
		if ( scene == null ) return false;

		switch ( ActiveKind )
		{
			case WorldContainerKind.Furnace:
			{
				var furnace = scene.GetAllComponents<VeggaFurnace>()
					.FirstOrDefault( f => f != null && f.IsValid() && f.ContainerId == ActiveId );
				if ( furnace == null ) return false;
				furnace.RequestDepositFromPlayer( fromSlot, count );
				return true;
			}
			case WorldContainerKind.StorageBox:
			{
				var box = scene.GetAllComponents<VeggaStorageBox>()
					.FirstOrDefault( b => b != null && b.IsValid() && b.ContainerId == ActiveId );
				if ( box == null ) return false;
				box.RequestDepositFromPlayer( fromSlot, count );
				return true;
			}
			default:
				return false;
		}
	}

	public static bool TryDepositFromPlayerInventoryToFurnaceSlot( VeggaInventory inv, int fromSlot, int count, int targetFurnaceSlot )
	{
		if ( inv == null || !inv.IsValid() ) return false;
		if ( !IsActive ) return false;
		if ( ActiveKind != WorldContainerKind.Furnace ) return false;
		if ( fromSlot < 0 || fromSlot >= inv.TotalSlots ) return false;
		if ( count <= 0 ) return false;
		if ( targetFurnaceSlot < 0 ) return false;

		var scene = Game.ActiveScene;
		if ( scene == null ) return false;

		var furnace = scene.GetAllComponents<VeggaFurnace>()
			.FirstOrDefault( f => f != null && f.IsValid() && f.ContainerId == ActiveId );
		if ( furnace == null ) return false;

		furnace.RequestDepositFromPlayerToSlot( fromSlot, count, targetFurnaceSlot );
		return true;
	}
}
