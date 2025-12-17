using System;
using Sandbox;

namespace Sandbox.UI;

public static class InventoryDropCommands
{
	static bool CanAct()
	{
		if ( !Game.IsPlaying ) return false;
		if ( Connection.Local == null ) return false;
		if ( VeggaChat.IsChatInputOpenGlobal ) return false;
		return true;
	}

	[ConCmd( "dropitemstack", Help = "Drop the full hovered stack from the inventory UI." )]
	public static void DropItemStack()
	{
		if ( !CanAct() ) return;
		var inv = VeggaInventory.Local;
		if ( inv == null || !inv.IsValid() ) return;
		if ( InventoryHudSlot.HoveredInventory != inv ) return;

		int slot = InventoryHudSlot.HoveredSlotIndex;
		if ( slot < 0 || slot >= inv.TotalSlots ) return;
		int itemId = inv.GetSlotItemId( slot );
		if ( itemId <= 0 ) return;
		int count = Math.Max( 1, inv.GetSlotCount( slot ) );
		inv.RequestDropFromSlot( slot, count );
	}

	[ConCmd( "dropitemsingle", Help = "Drop a single item from the hovered stack from the inventory UI." )]
	public static void DropItemSingle()
	{
		if ( !CanAct() ) return;
		var inv = VeggaInventory.Local;
		if ( inv == null || !inv.IsValid() ) return;
		if ( InventoryHudSlot.HoveredInventory != inv ) return;

		int slot = InventoryHudSlot.HoveredSlotIndex;
		if ( slot < 0 || slot >= inv.TotalSlots ) return;
		int itemId = inv.GetSlotItemId( slot );
		if ( itemId <= 0 ) return;
		inv.RequestDropFromSlot( slot, 1 );
	}
}
