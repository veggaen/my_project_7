using System;
using Sandbox;
using Sandbox.UI;

namespace Sandbox;

public sealed class VeggaEquipmentController : Component
{
	private static VeggaEquipmentController _local;

	private static VeggaEquipmentController ResolveFromRoot( GameObject root )
	{
		if ( root == null || !root.IsValid() )
			return null;

		var equipment = root.Components.Get<VeggaEquipmentController>( FindMode.InSelf | FindMode.InAncestors | FindMode.InDescendants );
		return equipment != null && equipment.IsValid() ? equipment : null;
	}

	public static VeggaEquipmentController Local
	{
		get
		{
			var localConn = Connection.Local;
			if ( localConn == null )
				return null;

			var movement = PlayerVeggaMovement.Local;
			var movementGo = movement?.GameObject;
			var resolved = ResolveFromRoot( movementGo );
			if ( resolved != null )
			{
				_local = resolved;
				return _local;
			}

			var stats = PlayerVeggaStats.Local;
			var statsGo = stats?.GameObject;
			resolved = ResolveFromRoot( statsGo );
			if ( resolved != null )
			{
				_local = resolved;
				return _local;
			}

			var inv = VeggaInventory.Local;
			var invGo = inv?.GameObject;
			resolved = ResolveFromRoot( invGo );
			if ( resolved != null )
			{
				_local = resolved;
				return _local;
			}

			var scene = Game.ActiveScene;
			if ( scene == null )
			{
				_local = null;
				return null;
			}

			foreach ( var eq in scene.GetAllComponents<VeggaEquipmentController>() )
			{
				if ( eq == null || !eq.IsValid() )
					continue;
				if ( eq.Network?.IsProxy == true )
					continue;
				if ( eq.GameObject?.Network?.Owner == localConn )
				{
					_local = eq;
					return _local;
				}
			}

			_local = null;
			return null;
		}
	}

	[Sync] public int ActiveHotbarSlot { get; private set; }
	[Sync] public VeggaHoldType HoldType { get; private set; } = VeggaHoldType.None;
	[Sync] public bool IsAiming { get; private set; }
	[Sync] public bool IsDualWielding { get; private set; }
	[Sync] public int ShotSequence { get; private set; }
	[Sync] public int ReloadSequence { get; private set; }
	[Sync] public bool IsReloading { get; private set; }
	[Sync] public int LastFireSide { get; private set; } = 1;
	[Sync] public int LeftHandMagazineAmmo { get; private set; }
	[Sync] public int RightHandMagazineAmmo { get; private set; }

	[Property, Group( "Weapons" )] public bool AutoReloadWhenEmpty { get; set; } = true;

	private VeggaInventory _inventory;
	private PlayerVeggaMovement _movement;
	private TimeSince _reloadStarted;
	private VeggaWeaponSpec _pendingReloadSpec;

	private GameObject _worldModelObject;
	private ModelRenderer _worldModelRenderer;
	private GameObject _secondaryWorldModelObject;
	private ModelRenderer _secondaryWorldModelRenderer;
	private int _worldModelItemId;
	private SkinnedModelRenderer _worldModelSkin;
	private GameObject _worldModelBoneL;
	private GameObject _worldModelBoneR;

	// Host-side rate limiting
	private TimeSince _sinceHostShot;
	private TimeSince _sinceHostMelee;
	private TimeSince _sinceLeftHandShot;
	private TimeSince _sinceRightHandShot;
	private int _runtimeWeaponItemId = int.MinValue;

	protected override void OnStart()
	{
		_inventory = Components.Get<VeggaInventory>();
		_movement = Components.Get<PlayerVeggaMovement>();
		_sinceHostShot = 999f;
		_sinceHostMelee = 999f;
		_sinceLeftHandShot = 999f;
		_sinceRightHandShot = 999f;
		_worldModelItemId = int.MinValue;
	}

	protected override void OnUpdate()
	{
		_inventory ??= Components.Get<VeggaInventory>();
		_movement ??= Components.Get<PlayerVeggaMovement>();

		// Always allow hotbar selection input for the owning client, even if this component
		// is currently a proxy. In some network setups the player pawn/components can be
		// proxies on the client, so we predict locally and request the host.
		if ( IsOwnedByLocalConnection() && _inventory != null && _inventory.IsValid() )
		{
			HandleSlotSelectionInput();
		}

		// Keep third-person weapon visuals up to date after any local slot changes so the
		// rendered weapon and HUD state are sourced from the same frame of equipment data.
		UpdateWorldModelVisual();

		// Local-owner input handling.
		// Be permissive about ownership checks: depending on spawn/ownership, Network.IsOwner
		// can be false even though this is the local player's pawn. We instead anchor to the
		// owning connection of the inventory/pawn.
		if ( !IsLocallyOwned() )
			return;

		if ( _inventory == null || !_inventory.IsValid() )
			return;

		// Hotbar selection was already handled above.

		// Respect global UI mouse authority for aim/fire/reload.
		if ( VeggaUiMouse.WantsUiMouse )
		{
			IsAiming = false;
			IsDualWielding = false;
			UpdateDerivedState();
			return;
		}

		var equippedItemId = _inventory.GetSlotItemId( ActiveHotbarSlot );
		var isWeapon = VeggaEquipmentCatalog.TryGetWeaponSpec( equippedItemId, out var weaponSpec );
		if ( isWeapon )
		{
			SyncWeaponRuntimeState( equippedItemId, weaponSpec );
		}
		else
		{
			IsDualWielding = false;
			IsAiming = false;
		}

		IsAiming = isWeapon && weaponSpec.SupportsAds && Input.Down( "Attack2" );

		// Complete reload after timer expires.
		if ( IsReloading && _reloadStarted >= _pendingReloadSpec.ReloadTime )
		{
			FinishReload();
		}

		if ( Input.Pressed( "Reload" ) && isWeapon && !IsReloading )
		{
			TryReloadLocal( weaponSpec );
		}

		if ( !IsReloading )
		{
			if ( isWeapon && weaponSpec.IsDualWield )
			{
				if ( Input.Pressed( "Attack1" ) )
					TryFireLocal( weaponSpec, GetLeadFireSide() );

				if ( Input.Pressed( "Attack2" ) )
					TryFireLocal( weaponSpec, -GetLeadFireSide() );
			}
			else if ( Input.Pressed( "Attack1" ) )
			{
				if ( isWeapon )
				{
					TryFireLocal( weaponSpec, GetLeadFireSide() );
				}
				else if ( VeggaEquipmentCatalog.TryGetMeleeDamage( equippedItemId, out var dmg, out var range ) )
				{
					RpcRequestMelee( GetRequesterId(), dmg, range );
				}
				else if ( VeggaEquipmentCatalog.IsTool( equippedItemId ) )
				{
					// Stub for tool items. (Build tools will live here.)
					Log.Info( "[Tool] Build hammer used (stub)." );
				}
			}
		}

		if ( AutoReloadWhenEmpty && isWeapon )
		{
			var mag = _inventory.GetSlotDurability( ActiveHotbarSlot );
			if ( mag <= 0 && Input.Pressed( "Attack1" ) )
			{
				TryReloadLocal( weaponSpec );
			}
		}

		UpdateDerivedState();
		UpdateWorldModelVisual();
	}

	public void SetActiveHotbarSlot( int slot )
	{
		_inventory ??= Components.Get<VeggaInventory>();
		if ( !IsOwnedByLocalConnection() )
			return;

		// -1 means "unequipped".
		if ( slot < -1 ) slot = -1;
		if ( slot > 8 ) slot = 8;
		// Predict locally for immediate UI feedback.
		ActiveHotbarSlot = slot;
		UpdateDerivedState();
		UpdateWorldModelVisual();

		// If we just equipped a weapon and the mag is empty, auto-load it immediately.
		// This makes "equip then shoot" feel responsive without requiring a manual reload.
		if ( _inventory != null && _inventory.IsValid() )
		{
			var equippedItemId = _inventory.GetSlotItemId( ActiveHotbarSlot );
			if ( AutoReloadWhenEmpty && VeggaEquipmentCatalog.TryGetWeaponSpec( equippedItemId, out var spec ) )
			{
				var mag = _inventory.GetSlotDurability( ActiveHotbarSlot );
				if ( mag <= 0 )
				{
					TryReloadLocal( spec );
				}
			}
		}

		// If we're not the host (or this component is a proxy), ask the host to apply.
		// This makes hotbar selection work in dedicated server / proxy-authoritative cases.
		if ( Connection.Local != null && !Networking.IsHost )
		{
			RpcRequestSetActiveHotbarSlot( Connection.Local.Id, slot );
		}
	}

	private void UpdateWorldModelVisual()
	{
		if ( _inventory == null || !_inventory.IsValid() )
		{
			DestroyWorldModel();
			_worldModelItemId = int.MinValue;
			return;
		}

		var itemId = GetEquippedItemId();
		if ( !VeggaEquipmentCatalog.TryGetWeaponSpec( itemId, out var spec ) || string.IsNullOrWhiteSpace( spec.WorldModelPath ) )
		{
			DestroyWorldModel();
			_worldModelItemId = int.MinValue;
			return;
		}

		if ( itemId == _worldModelItemId && _worldModelObject != null && _worldModelObject.IsValid() )
		{
			// Temporary fallback: keep the weapon attached to the stock right-hand hold
			// so the body pose and weapon presentation stay consistent until the custom
			// lead-side animgraph is authored.
			UpdateWorldModelAttachment( spec );

			// Still update visibility (first-person hiding).
			UpdateWorldModelVisibilityForLocalCamera();
			return;
		}

		_worldModelItemId = itemId;

		EnsureWorldModel();
		try
		{
			var model = Model.Load( spec.WorldModelPath );
			_worldModelRenderer.Model = model;
			if ( spec.IsDualWield )
			{
				EnsureSecondaryWorldModel();
				_secondaryWorldModelRenderer.Model = model;
			}
			else
			{
				DestroySecondaryWorldModel();
			}
		}
		catch
		{
			_worldModelRenderer.Model = null;
			DestroySecondaryWorldModel();
		}
		AttachWorldModelToHoldBone();
		UpdateWorldModelVisibilityForLocalCamera();
	}

	private void EnsureWorldModel()
	{
		if ( _worldModelObject != null && _worldModelObject.IsValid() && _worldModelRenderer != null && _worldModelRenderer.IsValid() )
			return;

		DestroyWorldModel();

		_worldModelObject = new GameObject( true, "weapon_worldmodel" );
		_worldModelObject.Parent = GameObject;
		_worldModelObject.Transform.LocalPosition = Vector3.Zero;
		_worldModelObject.Transform.LocalRotation = Rotation.Identity;
		_worldModelObject.Transform.ClearInterpolation();

		_worldModelRenderer = _worldModelObject.Components.Create<ModelRenderer>();
		_worldModelRenderer.RenderType = ModelRenderer.ShadowRenderType.On;
		_worldModelSkin = null;
		_worldModelBoneL = null;
		_worldModelBoneR = null;
	}

	private void EnsureSecondaryWorldModel()
	{
		if ( _secondaryWorldModelObject != null && _secondaryWorldModelObject.IsValid() && _secondaryWorldModelRenderer != null && _secondaryWorldModelRenderer.IsValid() )
			return;

		DestroySecondaryWorldModel();

		_secondaryWorldModelObject = new GameObject( true, "weapon_worldmodel_secondary" );
		_secondaryWorldModelObject.Parent = GameObject;
		_secondaryWorldModelObject.Transform.LocalPosition = Vector3.Zero;
		_secondaryWorldModelObject.Transform.LocalRotation = Rotation.Identity;
		_secondaryWorldModelObject.Transform.ClearInterpolation();

		_secondaryWorldModelRenderer = _secondaryWorldModelObject.Components.Create<ModelRenderer>();
		_secondaryWorldModelRenderer.RenderType = ModelRenderer.ShadowRenderType.On;
	}

	private void DestroyWorldModel()
	{
		if ( _worldModelObject != null && _worldModelObject.IsValid() )
			_worldModelObject.Destroy();
		_worldModelObject = null;
		_worldModelRenderer = null;
		DestroySecondaryWorldModel();
		_worldModelSkin = null;
		_worldModelBoneL = null;
		_worldModelBoneR = null;
	}

	private void DestroySecondaryWorldModel()
	{
		if ( _secondaryWorldModelObject != null && _secondaryWorldModelObject.IsValid() )
			_secondaryWorldModelObject.Destroy();
		_secondaryWorldModelObject = null;
		_secondaryWorldModelRenderer = null;
	}

	private void AttachWorldModelToHoldBone()
	{
		var body = _movement?.Body;
		if ( body == null || !body.IsValid() )
		{
			_worldModelObject.Parent = GameObject;
			if ( _secondaryWorldModelObject != null && _secondaryWorldModelObject.IsValid() )
				_secondaryWorldModelObject.Parent = GameObject;
			return;
		}

		_worldModelSkin = body.Components.Get<SkinnedModelRenderer>()
			?? body.Components.GetAll<SkinnedModelRenderer>( FindMode.InDescendants ).FirstOrDefault();
		if ( _worldModelSkin == null || !_worldModelSkin.IsValid() )
		{
			_worldModelObject.Parent = body;
			if ( _secondaryWorldModelObject != null && _secondaryWorldModelObject.IsValid() )
				_secondaryWorldModelObject.Parent = body;
			return;
		}

		// Ensure bone objects exist so GetBoneObject works.
		_worldModelSkin.CreateBoneObjects = true;
		_worldModelBoneL = _worldModelSkin.GetBoneObject( "hold_L" ) ?? _worldModelSkin.GetBoneObject( "hand_L" );
		_worldModelBoneR = _worldModelSkin.GetBoneObject( "hold_R" ) ?? _worldModelSkin.GetBoneObject( "hand_R" );

		// Keep the weapon object parented to the body so presentation remains body-anchored.
		_worldModelObject.Parent = body;
		_worldModelObject.Transform.ClearInterpolation();
		if ( _secondaryWorldModelObject != null && _secondaryWorldModelObject.IsValid() )
		{
			_secondaryWorldModelObject.Parent = body;
			_secondaryWorldModelObject.Transform.ClearInterpolation();
		}

		if ( TryGetActiveWeaponSpec( out var spec ) )
			UpdateWorldModelAttachment( spec );
	}

	private void UpdateWorldModelAttachment( VeggaWeaponSpec spec )
	{
		if ( _worldModelObject == null || !_worldModelObject.IsValid() )
			return;
		if ( _worldModelBoneR == null || !_worldModelBoneR.IsValid() )
			return;

		// Temporary fallback:
		// Keep the weapon on hold_R because the stock citizen pistol pose only has
		// a valid right-hand lead animation. The real left/right lead solution belongs
		// in a custom animgraph with authored poses, not in this controller.
		_worldModelObject.WorldPosition = _worldModelBoneR.WorldPosition;
		_worldModelObject.WorldRotation = _worldModelBoneR.WorldRotation;
		_worldModelObject.LocalScale = Vector3.One;
		_worldModelObject.Transform.ClearInterpolation();

		if ( spec.IsDualWield )
		{
			EnsureSecondaryWorldModel();
			if ( _secondaryWorldModelObject != null && _secondaryWorldModelObject.IsValid() && _worldModelBoneL != null && _worldModelBoneL.IsValid() )
			{
				_secondaryWorldModelObject.WorldPosition = _worldModelBoneL.WorldPosition;
				_secondaryWorldModelObject.WorldRotation = _worldModelBoneL.WorldRotation;
				_secondaryWorldModelObject.LocalScale = Vector3.One;
				_secondaryWorldModelObject.Transform.ClearInterpolation();
			}
		}
		else
		{
			DestroySecondaryWorldModel();
		}
	}

	private void UpdateWorldModelVisibilityForLocalCamera()
	{
		if ( _worldModelRenderer == null || !_worldModelRenderer.IsValid() )
			return;

		// Hide the third-person model for the local player while in first-person
		// to prevent clipping (viewmodel should be used instead).
		bool hideForFirstPerson = false;
		if ( IsLocallyOwned() && Scene != null )
		{
			var cam = Scene.Components.GetAll<CameraVeggaMovement>().FirstOrDefault();
			if ( cam != null && cam.IsValid() )
				hideForFirstPerson = cam.InFirstPerson;
		}

		_worldModelRenderer.RenderType = hideForFirstPerson
			? ModelRenderer.ShadowRenderType.Off
			: ModelRenderer.ShadowRenderType.On;

		if ( _secondaryWorldModelRenderer != null && _secondaryWorldModelRenderer.IsValid() )
		{
			_secondaryWorldModelRenderer.RenderType = hideForFirstPerson
				? ModelRenderer.ShadowRenderType.Off
				: ModelRenderer.ShadowRenderType.On;
		}
	}

	private bool IsOwnedByLocalConnection()
	{
		var local = Connection.Local;
		if ( local == null )
			return false;

		// Strong fallback: local player root.
		var localStats = PlayerVeggaStats.Local;
		if ( localStats != null && localStats.IsValid() && localStats.GameObject == GameObject )
			return true;

		// Strong fallback: if this equipment is on the same GameObject as the local inventory,
		// treat it as locally owned even if Network.Owner hasn't been set up yet.
		var localInv = VeggaInventory.Local;
		if ( localInv != null && localInv.IsValid() && localInv.GameObject == GameObject )
			return true;

		var goOwner = GameObject?.Network?.Owner;
		if ( goOwner != null && goOwner == local )
			return true;

		var invOwner = _inventory?.Network?.Owner;
		if ( invOwner != null && invOwner == local )
			return true;

		return false;
	}

	[Rpc.Broadcast]
	private void RpcRequestSetActiveHotbarSlot( Guid requesterId, int slot )
	{
		if ( !Networking.IsHost )
			return;
		if ( requesterId == Guid.Empty )
			return;
		if ( GameObject?.Network?.Owner?.Id != requesterId )
			return;

		slot = slot.Clamp( -1, 8 );
		ActiveHotbarSlot = slot;
		UpdateDerivedState();
	}

	private bool IsLocallyOwned()
	{
		var local = Connection.Local;
		if ( local == null )
			return false;

		if ( Network.IsProxy )
			return false;

		// Prefer explicit ownership via the pawn.
		var goOwner = GameObject?.Network?.Owner;
		if ( goOwner != null )
			return goOwner == local;

		// Fallback: if the inventory is explicitly owned by local, treat this as local.
		var invOwner = _inventory?.Network?.Owner;
		if ( invOwner != null )
			return invOwner == local && _inventory?.Network?.IsProxy != true;

		// Strong fallback: compare against the known local player singletons.
		try
		{
			var localStats = PlayerVeggaStats.Local;
			if ( localStats != null && localStats.IsValid() )
			{
				// Equipment lives on the player root in our prefab.
				if ( localStats.GameObject == GameObject )
					return true;
				var localEquip = localStats.GameObject?.Components.Get<VeggaEquipmentController>();
				if ( localEquip == this )
					return true;
			}

			var localInv = VeggaInventory.Local;
			if ( localInv != null && localInv.IsValid() && localInv.GameObject == GameObject )
				return true;
		}
		catch
		{
			// Best-effort only.
		}

		// Last resort: engine owner flag.
		return Network.IsOwner;
	}

	public bool CanAim()
	{
		if ( HoldType == VeggaHoldType.None )
			return false;

		if ( !TryGetActiveWeaponSpec( out var spec ) )
			return false;
		return spec.SupportsAds;
	}

	public float GetMovementSpeedMultiplier()
	{
		if ( !TryGetActiveWeaponSpec( out var spec ) )
			return 1f;

		return IsAiming ? spec.AimMoveSpeedMultiplier : 1f;
	}

	public float GetAdsFovMultiplier()
	{
		if ( !TryGetActiveWeaponSpec( out var spec ) )
			return 1f;

		return spec.SupportsAds ? spec.AdsFovMultiplier : 1f;
	}

	public bool TryGetActiveWeaponSpec( out VeggaWeaponSpec spec )
	{
		spec = default;
		if ( HoldType == VeggaHoldType.None )
			return false;

		if ( _inventory == null || !_inventory.IsValid() )
			return false;

		var itemId = _inventory.GetSlotItemId( ActiveHotbarSlot );
		return VeggaEquipmentCatalog.TryGetWeaponSpec( itemId, out spec );
	}

	public bool TryGetAmmoDisplay( out bool dualWield, out int leftMag, out int rightMag, out int magCap, out int reserve )
	{
		dualWield = false;
		leftMag = 0;
		rightMag = 0;
		magCap = 0;
		reserve = 0;

		if ( !TryGetActiveWeaponSpec( out var spec ) )
			return false;

		magCap = Math.Max( 0, spec.MagazineSize );
		reserve = Math.Max( 0, _inventory.GetItemCount( spec.AmmoItemId ) );
		dualWield = spec.IsDualWield;

		if ( dualWield )
		{
			leftMag = LeftHandMagazineAmmo.Clamp( 0, magCap );
			rightMag = RightHandMagazineAmmo.Clamp( 0, magCap );
		}
		else
		{
			rightMag = _inventory.GetSlotDurability( ActiveHotbarSlot ).Clamp( 0, magCap );
		}

		return true;
	}

	public int GetEquippedItemId()
	{
		if ( HoldType == VeggaHoldType.None )
			return 0;

		_inventory ??= Components.Get<VeggaInventory>();
		if ( _inventory == null || !_inventory.IsValid() )
			return 0;
		return _inventory.GetSlotItemId( ActiveHotbarSlot );
	}

	public bool TryGetWorldModelTransform( out Transform worldTransform )
	{
		worldTransform = default;
		if ( _worldModelObject == null || !_worldModelObject.IsValid() )
			return false;
		worldTransform = _worldModelObject.WorldTransform;
		return true;
	}

	private void HandleSlotSelectionInput()
	{
		static bool PressedSlot( int number, string actionName )
		{
			// Primary: action mapping from ProjectSettings/Input.config
			if ( Input.Pressed( actionName ) || Input.Pressed( actionName.ToLowerInvariant() ) )
				return true;

			// Fallback: raw keyboard key code (helps if UI/focus prevents action dispatch).
			// Input.config uses "1".."9" for KeyboardCode, so match that.
			return Input.Keyboard.Pressed( number.ToString() );
		}

		// Slots 1..9 map to hotbar indices 0..8.
		// Accept both cases to match other UI code (some actions are lower-cased by configs/users).
		if ( PressedSlot( 1, "Slot1" ) ) ToggleHotbarSlot( 0 );
		else if ( PressedSlot( 2, "Slot2" ) ) ToggleHotbarSlot( 1 );
		else if ( PressedSlot( 3, "Slot3" ) ) ToggleHotbarSlot( 2 );
		else if ( PressedSlot( 4, "Slot4" ) ) ToggleHotbarSlot( 3 );
		else if ( PressedSlot( 5, "Slot5" ) ) ToggleHotbarSlot( 4 );
		else if ( PressedSlot( 6, "Slot6" ) ) ToggleHotbarSlot( 5 );
		else if ( PressedSlot( 7, "Slot7" ) ) ToggleHotbarSlot( 6 );
		else if ( PressedSlot( 8, "Slot8" ) ) ToggleHotbarSlot( 7 );
		else if ( PressedSlot( 9, "Slot9" ) ) ToggleHotbarSlot( 8 );

		if ( Input.Pressed( "SlotPrev" ) )
			SetActiveHotbarSlot( (ActiveHotbarSlot + 8) % 9 );
		else if ( Input.Pressed( "SlotNext" ) )
			SetActiveHotbarSlot( (ActiveHotbarSlot + 1) % 9 );
	}

	private void ToggleHotbarSlot( int slotIndex )
	{
		if ( slotIndex < 0 || slotIndex > 8 )
			return;

		// Pressing the same key again unequips.
		SetActiveHotbarSlot( ActiveHotbarSlot == slotIndex ? -1 : slotIndex );
	}

	private void UpdateDerivedState()
	{
		if ( _inventory == null || !_inventory.IsValid() )
		{
			HoldType = VeggaHoldType.None;
			IsAiming = false;
			IsReloading = false;
			IsDualWielding = false;
			return;
		}

		var equippedItemId = _inventory.GetSlotItemId( ActiveHotbarSlot );
		HoldType = VeggaEquipmentCatalog.GetHoldType( equippedItemId );

		if ( HoldType == VeggaHoldType.None || !VeggaEquipmentCatalog.TryGetWeaponSpec( equippedItemId, out var spec ) )
		{
			IsAiming = false;
			IsReloading = false;
			IsDualWielding = false;
			return;
		}

		if ( IsReloading && _pendingReloadSpec.ItemId != equippedItemId )
			IsReloading = false;

		IsDualWielding = spec.IsDualWield;
	}

	private void TryFireLocal( VeggaWeaponSpec spec, int fireSide )
	{
		// Fire-rate gate client-side (feel). Host enforces its own too.
		var minDelay = 1f / MathF.Max( 0.001f, spec.FireRateRps );

		if ( fireSide < 0 )
		{
			if ( _sinceLeftHandShot < minDelay )
				return;
		}
		else
		{
			if ( _sinceRightHandShot < minDelay )
				return;
		}

		int magBefore;
		if ( spec.IsDualWield )
		{
			magBefore = fireSide < 0 ? LeftHandMagazineAmmo : RightHandMagazineAmmo;
			if ( magBefore <= 0 )
				return;

			if ( fireSide < 0 )
				LeftHandMagazineAmmo = magBefore - 1;
			else
				RightHandMagazineAmmo = magBefore - 1;
		}
		else
		{
			magBefore = _inventory.GetSlotDurability( ActiveHotbarSlot );
			if ( magBefore <= 0 )
				return;

			_inventory.SetSlotDurability( ActiveHotbarSlot, magBefore - 1 );
		}

		LastFireSide = fireSide;
		ShotSequence++;

		// Client-side feedback: sound + recoil.
		ApplyRecoil( spec, fireSide );
		PlayShootSound( spec );

		RpcRequestFire( GetRequesterId(), ActiveHotbarSlot, spec.ItemId, magBefore, fireSide );
		if ( fireSide < 0 )
			_sinceLeftHandShot = 0;
		else
			_sinceRightHandShot = 0;
	}

	private void ApplyRecoil( VeggaWeaponSpec spec, int fireSide )
	{
		if ( _movement == null ) return;
		// Kick the camera up and slightly sideways for recoil feel.
		var pitchKick = -spec.RecoilPitch;  // negative = aim up
		var yawKick = (Game.Random.Float() - 0.5f) * 2f * spec.RecoilYaw;
		if ( spec.IsDualWield )
		{
			yawKick += fireSide < 0 ? -0.15f : 0.15f;
		}
		_movement.TargetHeadAngle += new Angles( pitchKick, yawKick, 0f );
	}

	private void PlayShootSound( VeggaWeaponSpec spec )
	{
		if ( string.IsNullOrWhiteSpace( spec.ShootSound ) ) return;
		try
		{
			Sound.Play( spec.ShootSound, WorldPosition );
		}
		catch
		{
			// Sound resource may not exist yet.
		}
	}

	private Guid GetRequesterId()
	{
		return Connection.Local?.Id
			?? GameObject?.Network?.Owner?.Id
			?? Guid.Empty;
	}

	private void TryReloadLocal( VeggaWeaponSpec spec )
	{
		var max = spec.MagazineSize;
		var need = spec.IsDualWield
			? (max - LeftHandMagazineAmmo.Clamp( 0, max )) + (max - RightHandMagazineAmmo.Clamp( 0, max ))
			: max - _inventory.GetSlotDurability( ActiveHotbarSlot ).Clamp( 0, max );
		if ( need <= 0 )
			return;

		var have = _inventory.GetItemCount( spec.AmmoItemId );
		if ( have <= 0 )
			return;

		// Start reload — ammo transfer happens after the timer.
		_pendingReloadSpec = spec;
		_reloadStarted = 0;
		IsReloading = true;
		ReloadSequence++;
	}

	private void FinishReload()
	{
		IsReloading = false;
		var spec = _pendingReloadSpec;
		var max = spec.MagazineSize;
		var need = spec.IsDualWield
			? (max - LeftHandMagazineAmmo.Clamp( 0, max )) + (max - RightHandMagazineAmmo.Clamp( 0, max ))
			: max - _inventory.GetSlotDurability( ActiveHotbarSlot ).Clamp( 0, max );
		if ( need <= 0 ) return;

		var have = _inventory.GetItemCount( spec.AmmoItemId );
		var take = Math.Min( need, have );
		if ( take <= 0 ) return;

		if ( !_inventory.RemoveItem( spec.AmmoItemId, take ) )
			return;

		if ( spec.IsDualWield )
		{
			var left = LeftHandMagazineAmmo.Clamp( 0, max );
			var right = RightHandMagazineAmmo.Clamp( 0, max );
			var leftNeed = max - left;
			var leftTake = Math.Min( leftNeed, take );
			left += leftTake;
			take -= leftTake;

			var rightNeed = max - right;
			var rightTake = Math.Min( rightNeed, take );
			right += rightTake;

			LeftHandMagazineAmmo = left;
			RightHandMagazineAmmo = right;
		}
		else
		{
			var mag = _inventory.GetSlotDurability( ActiveHotbarSlot ).Clamp( 0, max );
			_inventory.SetSlotDurability( ActiveHotbarSlot, mag + take );
		}
	}

	[Rpc.Broadcast]
	private void RpcRequestFire( Guid requesterId, int slotIndex, int expectedItemId, int clientMagBefore, int fireSide )
	{
		if ( !Networking.IsHost )
			return;

		if ( requesterId == Guid.Empty )
			return;

		// Validate this equipment belongs to the requester.
		if ( GameObject?.Network?.Owner?.Id != requesterId )
			return;

		_inventory ??= Components.Get<VeggaInventory>();
		_movement ??= Components.Get<PlayerVeggaMovement>();

		if ( _inventory == null || !_inventory.IsValid() )
			return;

		slotIndex = slotIndex.Clamp( 0, 8 );
		var itemId = _inventory.GetSlotItemId( slotIndex );
		if ( itemId != expectedItemId )
			return;

		if ( !VeggaEquipmentCatalog.TryGetWeaponSpec( itemId, out var spec ) )
			return;

		var minDelay = 1f / MathF.Max( 0.001f, spec.FireRateRps );
		if ( fireSide < 0 )
		{
			if ( _sinceLeftHandShot < minDelay )
				return;
			_sinceLeftHandShot = 0;
		}
		else
		{
			if ( _sinceRightHandShot < minDelay )
				return;
			_sinceRightHandShot = 0;
		}

		// Host-side ammo check (best-effort; owner may have already consumed).
		var mag = spec.IsDualWield
			? (fireSide < 0 ? LeftHandMagazineAmmo : RightHandMagazineAmmo)
			: _inventory.GetSlotDurability( slotIndex );
		if ( mag <= 0 && clientMagBefore <= 0 )
			return;

		var (origin, dir) = GetShootOriginAndDirection();
		SpawnProjectile( requesterId, origin, dir, spec );
	}

	private void SyncWeaponRuntimeState( int equippedItemId, VeggaWeaponSpec spec )
	{
		IsDualWielding = spec.IsDualWield;

		if ( !spec.IsDualWield )
		{
			_runtimeWeaponItemId = equippedItemId;
			return;
		}

		if ( _runtimeWeaponItemId != equippedItemId )
		{
			if ( LeftHandMagazineAmmo <= 0 && RightHandMagazineAmmo <= 0 )
			{
				LeftHandMagazineAmmo = spec.MagazineSize;
				RightHandMagazineAmmo = spec.MagazineSize;
			}
			else
			{
				LeftHandMagazineAmmo = LeftHandMagazineAmmo.Clamp( 0, spec.MagazineSize );
				RightHandMagazineAmmo = RightHandMagazineAmmo.Clamp( 0, spec.MagazineSize );
			}
		}

		_runtimeWeaponItemId = equippedItemId;
	}

	private int GetLeadFireSide()
	{
		if ( TryGetActiveWeaponSpec( out var spec ) && !spec.IsDualWield )
		{
			// Until the authored left-lead graph exists, single-weapon firing stays on the
			// stock right-hand path even if the camera moves to the opposite shoulder.
			return 1;
		}

		if ( Scene != null )
		{
			var cam = Scene.Components.GetAll<CameraVeggaMovement>().FirstOrDefault();
			if ( cam != null && cam.IsValid() && !cam.InFirstPerson )
				return cam.TargetShoulderSide;
		}

		return LastFireSide == 0 ? 1 : LastFireSide;
	}

	[Rpc.Broadcast]
	private void RpcRequestMelee( Guid requesterId, float damage, float range )
	{
		if ( !Networking.IsHost )
			return;
		if ( requesterId == Guid.Empty )
			return;
		if ( GameObject?.Network?.Owner?.Id != requesterId )
			return;

		var minDelay = 0.35f;
		if ( _sinceHostMelee < minDelay )
			return;
		_sinceHostMelee = 0;

		var (origin, dir) = GetShootOriginAndDirection();
		var to = origin + dir * range.Clamp( 16f, 200f );

		var tr = Scene.Trace
			.Ray( origin, to )
			.WithoutTags( "trigger" )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		if ( tr.Hit && tr.GameObject.IsValid() )
		{
			const FindMode findMode = FindMode.InSelf | FindMode.InAncestors;
			if ( tr.GameObject.Components.TryGet<PlayerVeggaStats>( out var stats, findMode ) )
				stats.Damage( damage.Clamp( 1f, 200f ) );
		}
	}

	private (Vector3 origin, Vector3 dir) GetShootOriginAndDirection()
	{
		// Prefer head (matches camera/aim), fall back to body.
		var head = _movement?.Head;
		var origin = head != null && head.IsValid()
			? head.WorldPosition + head.WorldRotation.Up * 10f
			: WorldPosition + WorldRotation.Up * 60f;

		var dir = _movement != null
			? _movement.TargetHeadAngle.ToRotation().Forward
			: WorldRotation.Forward;

		return (origin, dir.Normal);
	}

	private void SpawnProjectile( Guid shooterId, Vector3 origin, Vector3 dir, VeggaWeaponSpec spec )
	{
		var scene = Scene;
		if ( scene == null )
			return;

		// Apply spread so bullets don't go dead-center every time.
		var spreadOffset = (Vector3.Random.Normal * spec.Spread);
		dir = (dir + spreadOffset).Normal;

		var go = new GameObject( true, "vegga_projectile" );
		go.WorldPosition = origin;
		go.WorldRotation = Rotation.LookAt( dir, Vector3.Up );
		go.Tags.Add( "projectile" );

		var proj = go.Components.Create<VeggaProjectile>();
		proj.ShooterId = shooterId;
		proj.Damage = spec.Damage;
		proj.Velocity = dir * spec.ProjectileSpeed;
		proj.Gravity = spec.BulletGravity;
		proj.Drag = spec.BulletDrag;
		proj.LifetimeSeconds = 3.0f;
		proj.SetShooter( GameObject );

		// Network spawn so all clients see the tracer.
		go.NetworkSpawn();
	}
}
