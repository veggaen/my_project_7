# 💀 Death & Respawn System - Implementation Plan

## 🎯 Goal:
Create a complete death and respawn system like OSRS/GMod where:
- Player dies when HP reaches 0
- Death screen appears
- Player respawns at spawn point after delay
- Items/inventory handling on death (optional)

---

## 📋 Phase 1: Basic Death Detection

### **1.1 Add Death State to PlayerVeggaStats**

```csharp
[Sync] public bool IsDead { get; private set; }
public event Action OnDeath;
public event Action OnRespawn;

protected override void OnUpdate()
{
    if ( Health <= 0 && !IsDead )
    {
        Die();
    }
}

[Broadcast]
public void Die()
{
    if ( IsDead ) return;
    
    IsDead = true;
    OnDeath?.Invoke();
    Log.Info( $"💀 {GameObject.Name} died!" );
    
    // Disable movement
    var movement = GameObject.Components.Get<PlayerVeggaMovement>();
    if ( movement != null )
        movement.Enabled = false;
    
    // Play death animation/effects
    // TODO: Add death effects
    
    // Respawn after delay
    _ = RespawnAfterDelay( 5f );
}

async Task RespawnAfterDelay( float delay )
{
    await Task.DelaySeconds( delay );
    Respawn();
}

[Broadcast]
public void Respawn()
{
    IsDead = false;
    Health = MaxHealth;
    Armor = InitArmor;
    
    OnRespawn?.Invoke();
    Log.Info( $"✨ {GameObject.Name} respawned!" );
    
    // Re-enable movement
    var movement = GameObject.Components.Get<PlayerVeggaMovement>();
    if ( movement != null )
        movement.Enabled = true;
    
    // Teleport to spawn point
    TeleportToSpawnPoint();
}

void TeleportToSpawnPoint()
{
    // Find spawn point in scene
    var spawnPoint = Scene.GetAllComponents<SpawnPoint>()
        .OrderBy( x => Guid.NewGuid() ) // Random spawn
        .FirstOrDefault();
    
    if ( spawnPoint != null )
    {
        GameObject.WorldPosition = spawnPoint.WorldPosition;
        GameObject.WorldRotation = spawnPoint.WorldRotation;
    }
}
```

---

## 📋 Phase 2: Death Screen UI

### **2.1 Create DeathScreen.razor**

```razor
@using Sandbox
@using Sandbox.UI
@inherits PanelComponent

<root>
    @if ( _isDead )
    {
        <div class="death-screen">
            <div class="death-content">
                <h1>You Died</h1>
                <p>Respawning in @_respawnTime seconds...</p>
                <div class="death-stats">
                    <p>Killed by: @_killedBy</p>
                    <p>Survived: @_survivalTime</p>
                </div>
            </div>
        </div>
    }
</root>

@code
{
    bool _isDead = false;
    int _respawnTime = 5;
    string _killedBy = "Unknown";
    string _survivalTime = "0:00";

    protected override void OnEnabled()
    {
        var player = PlayerVeggaStats.Local;
        if ( player != null )
        {
            player.OnDeath += OnPlayerDeath;
            player.OnRespawn += OnPlayerRespawn;
        }
    }

    void OnPlayerDeath()
    {
        _isDead = true;
        _respawnTime = 5;
        StateHasChanged();
        
        // Countdown timer
        _ = CountdownRespawn();
    }

    void OnPlayerRespawn()
    {
        _isDead = false;
        StateHasChanged();
    }

    async Task CountdownRespawn()
    {
        while ( _respawnTime > 0 && _isDead )
        {
            await Task.DelaySeconds( 1f );
            _respawnTime--;
            StateHasChanged();
        }
    }
}
```

---

## 📋 Phase 3: Spawn Points

### **3.1 Create SpawnPoint Component**

```csharp
public sealed class SpawnPoint : Component
{
    [Property] public bool IsEnabled { get; set; } = true;
    [Property] public string SpawnGroup { get; set; } = "default";
    
    protected override void DrawGizmos()
    {
        if ( !IsEnabled ) return;
        
        Gizmo.Draw.Color = Color.Green;
        Gizmo.Draw.LineSphere( Vector3.Zero, 16f );
        Gizmo.Draw.Arrow( Vector3.Zero, Vector3.Forward * 32f, 8f );
    }
}
```

---

## 🎯 Next Features to Add:

### **1. Kill Feed**
- Show who killed who
- Show death messages
- Scrolling feed in top-right

### **2. Death Drops**
- Drop items on death (optional)
- Corpse/ragdoll
- Loot system

### **3. Respawn Options**
- Choose spawn point
- Spectate mode
- Team spawns

### **4. Death Effects**
- Screen fade to black
- Death sound
- Ragdoll physics
- Camera spectate

---

## 🚀 Quick Implementation:

1. Add death detection to PlayerVeggaStats
2. Create DeathScreen UI component
3. Create SpawnPoint component
4. Add spawn points to your map
5. Test by setting HP to 0!

---

**This gives you a solid foundation for death/respawn!** 💀✨

