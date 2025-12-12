# 🎨 Inspector Attributes Guide

## ✅ **What We Already Fixed:**

### **Multiplayer Networking:**
- ✅ Added `[Sync]` to `IsCrouching`, `IsSprinting`, `TargetBodyAngle`
- ✅ Split input processing (owner only) from animations (all clients)
- ✅ Camera auto-links to local player
- ✅ Animations now work in multiplayer!

---

## 📋 **Useful Inspector Attributes:**

### **1. Organization:**

```csharp
// Group related properties
[Property, Group( "Movement Speeds" )]
public float BaseSpeed { get; set; } = 45.0f;

// Feature tabs (optional sections)
[Property, Feature( "Advanced" )]
public float AdvancedSetting { get; set; }

// Toggle groups (enable/disable whole section)
[Property, ToggleGroup( "EnableFeature" )]
public bool EnableFeature { get; set; }
```

### **2. Sliders & Ranges:**

```csharp
// Range slider with step
[Property, Range( 0f, 100f, 1f )]
public float Health { get; set; } = 100f;

// Clamped without slider
[Property, Range( 0f, 100f, 1f, clamped: true, slider: false )]
public float Value { get; set; }
```

### **3. Display Names:**

```csharp
// Custom title
[Property, Title( "HP" )]
public float Health { get; set; }

// Description tooltip
[Property, Description( "Maximum health points" )]
public float MaxHealth { get; set; }

// Read-only display
[Property, ReadOnly]
public int CurrentLevel { get; set; }
```

### **4. Input & Assets:**

```csharp
// Input action dropdown
[Property, InputAction]
public string JumpButton { get; set; } = "Jump";

// Image file picker
[Property, ImageAssetPath]
public string IconPath { get; set; }

// Font dropdown
[Property, FontName]
public string FontName { get; set; }
```

### **5. Conditional Display:**

```csharp
// Show only if condition is true
[Property, ShowIf( nameof(IsEnabled), true )]
public float Value { get; set; }

// Hide if condition is true
[Property, HideIf( nameof(IsDisabled), true )]
public float OtherValue { get; set; }
```

### **6. Text Fields:**

```csharp
// Multi-line text
[Property, TextArea]
public string Description { get; set; }
```

---

## 🎯 **Recommended Cleanup for PlayerVeggaMovement:**

Add these attributes to make the inspector cleaner:

```csharp
// References
[Property, Group( "References" )]
public GameObject Head { get; set; }

// Movement Speeds with sliders
[Property, Group( "Movement Speeds" ), Range( 0f, 100f, 1f ), Title( "Base Speed (WASD)" )]
public float BaseSpeed { get; set; } = 45.0f;

[Property, Group( "Movement Speeds" ), Range( 0f, 150f, 1f ), Title( "Run Speed (Shift)" )]
public float RunSpeed { get; set; } = 65.0f;

// Physics
[Property, Group( "Physics" ), Range( 0f, 20f, 0.1f )]
public float GroundControl { get; set; } = 5.0f;

// Body Rotation
[Property, Group( "Body Rotation" ), Range( 0f, 50f, 0.5f ), Title( "Turn Speed (Moving)" )]
public float BodyTurnSpeedMoving { get; set; } = 10.0f;

// Deprecated (hidden)
[Property, Group( "Deprecated" ), ReadOnly]
public float Speed { get; set; } = 160.0f;
```

---

## 🧪 **Test Your Multiplayer:**

1. **F7** - Compile
2. **F5** - Start server
3. **Walk, crouch, sprint** - Should work normally
4. **Launch → Join Local Server** - Open second client
5. **Verify:**
   - ✅ Other player sees your walk/run animations
   - ✅ Other player sees your crouch
   - ✅ Other player sees your body rotation
   - ✅ Each player controls their own camera

---

## 📚 **Key Multiplayer Rules:**

### **What to [Sync]:**
- ✅ Animation states (IsCrouching, IsSprinting)
- ✅ Body rotation (TargetBodyAngle)
- ✅ Player stats (Health, Armor, Money) - already done!

### **What NOT to [Sync]:**
- ❌ Input (Input.Down, Input.Pressed)
- ❌ Camera settings
- ❌ UI state
- ❌ Internal calculations (WishVelocity)

### **Code Structure:**
```csharp
protected override void OnUpdate()
{
    // ✅ Owner only: Process input
    if ( !IsProxy )
    {
        UpdateCrouch();
        IsSprinting = Input.Down( "Run" );
    }

    // ✅ All clients: Run animations (using synced data)
    RotateBody();
    UpdateAnimation();
}
```

---

**Your multiplayer is now working!** 🎮✨

