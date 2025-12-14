using Sandbox;
using System;
using Sandbox.Money;

namespace Sandbox;

/// <summary>
/// Gold bar smelting helper attached to the player.
///
/// Design:
/// - A 200g bar smelts into 200 gold coins.
/// - Each gold coin is worth $140 (fiat), but this smelter produces coins.
/// - Hand crafting: 1 second per coin.
/// - Forge crafting (standing in a PlayerForgeAura): much faster,
///   using PlayerVeggaStats.SmeltSpeedMultiplier (e.g. 100x at level 1).
/// - Smelting is interruptible: remaining grams are stored per bar in
///   VeggaInventory's durability, and other players can see/trade the
///   partially-smelted bar.
///
/// All authority runs on the host; clients see synced money, XP and
/// per-item durability via VeggaInventory.
/// </summary>
public sealed class PlayerGoldSmelter : Component
{
	[Property] public PlayerVeggaStats Stats { get; set; }
	[Property] public PlayerVeggaSkills Skills { get; set; }
	[Property] public VeggaInventory Inventory { get; set; }

	/// <summary>
	/// Base coins per second when hand-crafting away from any forge.
	/// Spec: 1 coin/sec =&gt; 1 second per coin.
	/// </summary>
	[Property] public float BaseHandCoinsPerSecond { get; set; } = 1f;

	/// <summary>
	/// Flat Crafting XP awarded per fully smelted 200g bar.
	/// (Keeps the system simple; we can move this to a furnace station later.)
	/// </summary>
	[Property] public int CraftingXpPerBar { get; set; } = 250;

	private int _activeSlot = -1;
	private int _gramsRemainingInJob;
	private bool _useForgeSpeed;
	private bool _awardedXpForBar;

	[Sync]
	public bool IsSmelting { get; private set; }
	private float _smeltProgress;
	private RealTimeSince _timeSinceLastTick;

	protected override void OnStart()
	{
		Stats ??= GameObject.Components.Get<PlayerVeggaStats>();
		Skills ??= GameObject.Components.Get<PlayerVeggaSkills>();
		Inventory ??= GameObject.Components.Get<VeggaInventory>();
	}

	/// <summary>
	/// Start smelting a single gold bar from the first matching slot.
	/// If useForgeSpeed is true and the player has a forge buff, the
	/// smelt speed is multiplied by Stats.SmeltSpeedMultiplier; otherwise
	/// hand speed (1 coin/sec) is used.
	/// </summary>
	public void StartSmeltingFirstGoldBar( bool useForgeSpeed )
	{
		Stats ??= GameObject.Components.Get<PlayerVeggaStats>();
		Skills ??= GameObject.Components.Get<PlayerVeggaSkills>();
		Inventory ??= GameObject.Components.Get<VeggaInventory>();

		if ( Inventory == null || !Inventory.IsValid() )
		{
			Log.Warning( "[GoldSmelter] No VeggaInventory found on player." );
			return;
		}

		const int GoldBarItemId = 100;
		var def = VeggaItemRegistry.Get( GoldBarItemId );
		int maxGrams = def?.MaxGrams ?? 0;
		if ( maxGrams <= 0 )
		{
			Log.Warning( "[GoldSmelter] Gold bar definition missing MaxGrams." );
			return;
		}

		// Ensure we have an actual single-bar durable slot to work with.
		if ( !Inventory.TryExtractOneDurable( GoldBarItemId, maxGrams, out int slot ) )
		{
			Log.Warning( "[GoldSmelter] No gold bar found in inventory." );
			return;
		}

		// Determine remaining grams for this bar
		int grams = Inventory.GetSlotDurability( slot );
		if ( grams <= 0 )
		{
			Log.Warning( "[GoldSmelter] Gold bar has no remaining grams." );
			return;
		}

		_activeSlot = slot;
		_gramsRemainingInJob = grams;
		_useForgeSpeed = useForgeSpeed;
		IsSmelting = true;
		_smeltProgress = 0f;
		_timeSinceLastTick = 0;
		_awardedXpForBar = false;

		var name = Stats?.Network?.Owner?.DisplayName ?? GameObject.Name;
		Log.Info( $"[GoldSmelter] Smelting started for {name}: slot={slot}, grams={grams}, useForge={useForgeSpeed}" );
	}

	protected override void OnUpdate()
	{
		// Only the host should drive money/XP changes.
		if ( !Networking.IsHost )
			return;

		if ( !IsSmelting || _activeSlot < 0 || _gramsRemainingInJob <= 0 )
			return;

		if ( Stats == null || !Stats.IsValid() )
		{
			IsSmelting = false;
			return;
		}

		if ( BaseHandCoinsPerSecond <= 0f )
			return;

		// Convert real time since last tick into fractional coins, adjusted
		// by the current smelt speed multiplier (hand vs forge).
		float dt = _timeSinceLastTick;
		_timeSinceLastTick = 0;

		if ( dt <= 0f )
			return;

		float speedMultiplier = Stats?.GetSmeltSpeedMultiplier( _useForgeSpeed ) ?? 1f;
		float coinsPerSecond = BaseHandCoinsPerSecond * speedMultiplier;

		_smeltProgress += dt * coinsPerSecond;
		int coinsThisFrame = Math.Min( _gramsRemainingInJob, (int)_smeltProgress );

		if ( coinsThisFrame <= 0 )
			return;

		_smeltProgress -= coinsThisFrame;
		_gramsRemainingInJob -= coinsThisFrame;

		// Mint gold coins (item 2) rather than adding fiat money.
		if ( Inventory != null && Inventory.IsValid() )
		{
			Inventory.AddItem( VeggaCurrency.GoldCoinItemId, coinsThisFrame );
		}

		// Update the bar's remaining grams in inventory; if depleted,
		// remove the item entirely.
		if ( Inventory != null && Inventory.IsValid() && _activeSlot >= 0 )
		{
			int currentDur = Inventory.GetSlotDurability( _activeSlot );
			int newDur = currentDur - coinsThisFrame;
			if ( newDur <= 0 )
			{
				// Remove the specific bar slot when fully consumed
				Inventory.SetSlotDurability( _activeSlot, 0 );
				Inventory.RemoveFromSlot( _activeSlot, 1 );

				// Flat XP per completed bar.
				if ( !_awardedXpForBar && Skills != null && CraftingXpPerBar > 0 )
				{
					Skills.AddXp( SkillId.Crafting, CraftingXpPerBar );
					_awardedXpForBar = true;
				}

				_activeSlot = -1;
				_gramsRemainingInJob = 0;
				IsSmelting = false;
				var name = Stats.Network?.Owner?.DisplayName ?? GameObject.Name;
				Log.Info( $"[GoldSmelter] Gold bar fully smelted for {name}." );
			}
			else
			{
				Inventory.SetSlotDurability( _activeSlot, newDur );
			}
		}

		// Stop the job if we've smelted as much as we planned this run
		if ( _gramsRemainingInJob <= 0 )
		{
			IsSmelting = false;
		}
	}
}
