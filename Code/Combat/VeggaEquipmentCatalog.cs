using System;
using Sandbox;

namespace Sandbox;

public readonly record struct VeggaWeaponSpec(
	int ItemId,
	VeggaHoldType HoldType,
	int AmmoItemId,
	int MagazineSize,
	float Damage,
	float ProjectileSpeed,
	float FireRateRps,
	string WorldModelPath,
	string ViewModelPath,
	float ReloadTime = 1.8f,
	float Spread = 0.015f,
	float RecoilPitch = 1.5f,
	float RecoilYaw = 0.4f,
	string ShootSound = "sounds/weapon_shoot.sound",
	string MuzzleFlashParticle = "",
	float BulletGravity = 300f,
	float BulletDrag = 0.002f
);

public static class VeggaEquipmentCatalog
{
	public static VeggaHoldType GetHoldType( int itemId )
		=> itemId switch
		{
			VeggaItemIds.Pistol9mm => VeggaHoldType.Pistol,
			VeggaItemIds.Rifle556 => VeggaHoldType.Rifle,
			VeggaItemIds.Knife => VeggaHoldType.Melee,
			VeggaItemIds.BuildHammer => VeggaHoldType.Tool,
			_ => VeggaHoldType.None
		};

	public static bool IsWeapon( int itemId )
		=> itemId == VeggaItemIds.Pistol9mm || itemId == VeggaItemIds.Rifle556;

	public static bool IsMelee( int itemId )
		=> itemId == VeggaItemIds.Knife;

	public static bool IsTool( int itemId )
		=> itemId == VeggaItemIds.BuildHammer;

	public static bool TryGetWeaponSpec( int itemId, out VeggaWeaponSpec spec )
	{
		spec = default;

		switch ( itemId )
		{
			case VeggaItemIds.Pistol9mm:
				spec = new VeggaWeaponSpec(
					ItemId: itemId,
					HoldType: VeggaHoldType.Pistol,
					AmmoItemId: VeggaItemIds.Ammo9mm,
					MagazineSize: 12,
					Damage: 12f,
					ProjectileSpeed: 4200f,
					FireRateRps: 6f,
					WorldModelPath: "models/weapons/sbox_pistol_usp/w_usp.vmdl",
					ViewModelPath: "models/weapons/sbox_pistol_usp/v_usp.vmdl"
				);
				return true;

			case VeggaItemIds.Rifle556:
				spec = new VeggaWeaponSpec(
					ItemId: itemId,
					HoldType: VeggaHoldType.Rifle,
						// MP5 uses 9mm (same as pistol) per design.
						AmmoItemId: VeggaItemIds.Ammo9mm,
					MagazineSize: 30,
					Damage: 9f,
					ProjectileSpeed: 6800f,
					FireRateRps: 10f,
						WorldModelPath: "models/weapons/sbox_smg_mp5/w_mp5.vmdl",
						ViewModelPath: "models/weapons/sbox_smg_mp5/v_mp5.vmdl"
				);
				return true;
		}

		return false;
	}

	public static bool TryGetMeleeDamage( int itemId, out float damage, out float range )
	{
		damage = 0f;
		range = 0f;

		if ( itemId == VeggaItemIds.Knife )
		{
			damage = 20f;
			range = 80f;
			return true;
		}

		return false;
	}
}
