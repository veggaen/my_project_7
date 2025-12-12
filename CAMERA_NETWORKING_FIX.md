# 📷 Camera Networking Fix - Multiplayer Support

## 🐛 The Problem

**Symptom:**
- Player 1 joins → Camera works fine
- Player 2 joins → **Player 1's camera switches to Player 2's view!**
- Both players see from Player 2's perspective

**Root Cause:**
Every player prefab had `IsMainCamera: true` on their CameraComponent. When Player 2 spawned, their camera ALSO claimed to be the "main camera", overriding Player 1's camera.

s&box only allows **ONE main camera per client**. If multiple cameras have `IsMainCamera: true`, the **last one wins**.

---

## ✅ The Solution

### **Dynamic Camera Ownership**

Each player's camera should **only be the main camera for its owner**, not for everyone.

**Key Concept:**
- `IsProxy == false` → This is YOUR player (local)
- `IsProxy == true` → This is SOMEONE ELSE's player (remote)

**Fix:**
```csharp
// In CameraVeggaMovement.cs
protected override void OnAwake()
{
    _camera = Components.Get<CameraComponent>();
    
    if ( _camera is not null )
    {
        // 🎯 Only set as main camera if this is the local player
        _camera.IsMainCamera = IsProxy == false;
    }
}
```

---

## 🔧 What Was Changed

### 1. **CameraVeggaMovement.cs**

**Added:**
- ✅ Dynamic `IsMainCamera` assignment based on `IsProxy`
- ✅ Input blocking for proxy players (other players don't respond to YOUR mouse)
- ✅ Logging to verify camera ownership

**Before:**
```csharp
protected override void OnAwake()
{
    _camera = Components.Get<CameraComponent>();
    _camera.FieldOfView = BaseFov;
}

protected override void OnUpdate()
{
    HandleInput(); // ❌ ALL players respond to input!
    // Mouse look code...
}
```

**After:**
```csharp
protected override void OnAwake()
{
    _camera = Components.Get<CameraComponent>();
    _camera.FieldOfView = BaseFov;
    
    // 🎯 Only YOUR camera is the main camera
    _camera.IsMainCamera = IsProxy == false;
}

protected override void OnStart()
{
    // Double-check after network initialization
    if ( _camera is not null )
    {
        _camera.IsMainCamera = IsProxy == false;
        Log.Info( $"📷 Camera: IsMainCamera={_camera.IsMainCamera}, IsProxy={IsProxy}" );
    }
}

protected override void OnUpdate()
{
    // 🎯 Only process input for YOUR player
    if ( !IsProxy )
    {
        HandleInput();
        // Mouse look code...
    }
}
```

### 2. **player_vegga.prefab**

**Changed:**
```json
"IsMainCamera": false  // ← Changed from true
```

**Why?**
- Prefab starts with `IsMainCamera: false`
- Code sets it to `true` dynamically for the owner
- This prevents conflicts during spawning

---

## 🎮 How It Works Now

### **Player 1 Joins:**
1. Player 1's prefab spawns
2. `IsProxy == false` (it's YOUR player)
3. Camera sets `IsMainCamera = true`
4. ✅ Player 1 sees from their own camera

### **Player 2 Joins:**
1. Player 2's prefab spawns
2. **On Player 1's client:**
   - Player 2's prefab has `IsProxy == true` (it's someone else)
   - Camera sets `IsMainCamera = false`
   - ✅ Player 1 still sees from their own camera
3. **On Player 2's client:**
   - Player 2's prefab has `IsProxy == false` (it's YOUR player)
   - Camera sets `IsMainCamera = true`
   - ✅ Player 2 sees from their own camera

**Result:** Each player sees from their own camera! 🎉

---

## 🧪 Testing

### **Test 1: Single Player**
1. Start server (F5)
2. Check console for: `📷 Camera: IsMainCamera=True, IsProxy=False`
3. ✅ Camera should work normally

### **Test 2: Two Players**
1. Start server (F5)
2. Open second client (Launch → Join Local Server)
3. Check console on BOTH clients:
   - **Player 1:** `📷 Camera: IsMainCamera=True, IsProxy=False`
   - **Player 2:** `📷 Camera: IsMainCamera=True, IsProxy=False`
4. ✅ Each player should see from their own perspective
5. ✅ Moving mouse on Player 1 should NOT affect Player 2's camera

---

## 📚 s&box Networking Concepts

### **IsProxy Property**
Every networked Component has an `IsProxy` property:
- `IsProxy == false` → This object is owned by the local player
- `IsProxy == true` → This object is owned by someone else (remote)

### **Camera Networking**
- Each client renders ONE main camera (`IsMainCamera: true`)
- Other players' cameras should have `IsMainCamera: false` on your client
- Use `IsProxy` to determine ownership

### **Input Handling**
- Only process input (`Input.MouseDelta`, `Input.Pressed`, etc.) when `IsProxy == false`
- Proxy objects should only receive networked data, not local input

---

## 🔍 Debugging

### **Check Camera Ownership:**
```csharp
Log.Info( $"Camera: IsMain={_camera.IsMainCamera}, IsProxy={IsProxy}, Owner={GameObject.Network.OwnerConnection?.DisplayName}" );
```

### **Common Issues:**

**"Both players see the same view"**
- ✅ Check `IsMainCamera` is set dynamically
- ✅ Check `IsProxy` is being checked correctly

**"Camera doesn't respond to input"**
- ✅ Make sure `IsProxy == false` for your player
- ✅ Check input code is inside `if ( !IsProxy )` block

**"Other player's camera moves with my mouse"**
- ✅ Make sure input handling checks `IsProxy`
- ✅ Verify `NetworkMode` is set to `2` (Owner) on camera GameObject

---

## ✅ Summary

**The Fix:**
1. ✅ Set `IsMainCamera` dynamically based on `IsProxy`
2. ✅ Block input for proxy players
3. ✅ Set prefab default to `IsMainCamera: false`

**Result:**
- ✅ Each player sees from their own camera
- ✅ Mouse input only affects your own camera
- ✅ Supports unlimited players!

**Your game now has proper multiplayer camera networking!** 🎮🎉

