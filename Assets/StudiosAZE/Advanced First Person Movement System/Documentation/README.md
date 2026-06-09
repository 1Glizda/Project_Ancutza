# Advanced FPS Movement System

**Version:** 1.0  
**Author:** AZE  
**Unity Version:** 2022.3+ (Recommended)

---

## Overview

The **Advanced First Person Movement System** is a robust, scalable character controller built on a **Finite State Machine (FSM)** architecture. It provides a fluid First-Person experience with built-in physics interactions, procedural camera animations ("game juice"), and a modular codebase designed for easy extension.

### Key Features
* **FSM Architecture:** Clean state separation (Idle, Walk, Sprint, Air, Crouch, Dodge).
* **Advanced Jump Mechanics:** Includes Coyote Time and Jump Buffering for precise and responsive platforming.
* **Physics-Based:** Custom gravity and ground detection.
* **Procedural Camera Effects:** Dynamic Head Bob, Tilt, and FOV kicks powered by Cinemachine.
* **Interaction System:** Physics-based pushing of dynamic objects.
* **New Input System:** Fully integrated with Unity's `com.unity.inputsystem`.

---

## 1. Dependencies (IMPORTANT)

**Before using this asset, you must install the following packages:**

Go to **Window > Package Manager > Unity Registry** and install:

1.  **Input System** (`com.unity.inputsystem`)
    * *Critical Step:* Go to **Project Settings > Player > Active Input Handling** and set it to **"Input System Package (New)"** or **"Both"**.
2.  **Cinemachine** (`com.unity.cinemachine`)

> **Note:** The demo scene scripts will **not compile** or function correctly without these packages installed.

---

## 2. Quick Setup

1.  **Import the Package:** Ensure all files are imported into your project folder.
2.  **Setup Scene:**
    * Create a new scene or open an existing one.
    * Ensure there is a floor with a **Collider** for the player to walk on.
3.  **Add the Player:**
    * Navigate to `Assets/AZE/PlayerMovementFPS/Prefabs/`.
    * Drag and drop the **Player** prefab into your scene.
4.  **Configure Inputs:**
    * Select the `Player` object in the Hierarchy.
    * Locate the `PlayerInputHandler` component in the Inspector.
    * Verify that the **Input Action References** are assigned (Move, Look, Jump, Sprint, Crouch, Dodge).
    * *If empty:* Drag the actions from `Assets/AZE/PlayerMovementFPS/Inputs/InputSystem_Actions` into the respective slots.

---

## 3. Architecture Overview

This asset uses a **Finite State Machine (FSM)** to handle movement logic. This ensures that code remains readable and scalable as you add more mechanics.

### Core Scripts
* **`PlayerMovementStateMachine.cs` (Context):** The central controller. Handles references, physics (Gravity, Ground Checks), and shared data.
* **`PlayerStateFactory.cs`:** Manages the lifecycle and instantiation of states.
* **`PlayerBaseState.cs`:** Abstract base class defining the contract (`Enter`, `Update`, `Exit`).

### Available States
* **Idle:** Stationary state. Handles transitions to movement.
* **Walk:** Standard movement logic.
* **Sprint:** Accelerated movement logic.
* **Air:** Handles falling physics, gravity application, and air control.
* **Jump:** Applies instantaneous vertical force.
* **Crouch:** Modifies character height and camera position.
* **Dodge:** Directional dash with cooldown management.

### Camera & Interaction
* **`PlayerCameraController.cs`:** Handles input look (Mouse/Gamepad), clamping, and sensitivity.
* **`CameraEffects.cs`:** Adds polish (Head Bob, Dutch Tilt, FOV Kick).
* **`CharacterPushInteraction.cs`:** Allows the player to push Rigidbody objects based on mass/velocity.

---

## 4. Configuration & Tuning

Settings are organized by component in the Inspector.

### Player Movement State Machine
* **General Toggles:**
    * `Use Jump`: Enable/Disable jumping.
    * `Use Crouch`: Enable/Disable crouching.
    * `Use Dodge`: Enable/Disable the dash ability.
* **Speed Settings:**
    * `Walk Speed` / `Run Speed` / `Crouch Speed`: Movement velocities.
    * `Movement Smoothing`: Interpolation for acceleration/deceleration.
* **Physics:**
    * `Gravity`: Custom gravity force (Default: -15).
* **Jump Settings:** 
    * `Jump Force`: Upward velocity applied on jump.
    * `Coyote Time`: The duration (in seconds) the player can still jump after walking off a ledge.
    * `Jump Buffer Time`: The duration (in seconds) a jump input will be stored before hitting the ground.
* **Dodge Settings:**
    * `Dodge Speed`: Velocity during dash.
    * `Dodge Duration`: Duration of the dash force.
    * `Dodge Cooldown`: Time required between dashes.
* **Crouch Settings:**
    * `Crouch Transition Speed`: Smoothness of height change.
    * `Camera Offset`: Camera height adjustment when crouching.

### Camera Effects
* **Head Bob:**
    * `Use Head Bob`: Toggle on/off.
    * *Settings:* Adjust Amplitude, Frequency, and Smoothing.
* **Camera Tilt (Dutch):**
    * `Use Tilt`: Toggle on/off.
    * *Settings:* Max Tilt Angle (e.g., 1.5°) and Smoothing.
* **Dynamic FOV:**
    * `Use FOV Kick`: Toggle on/off (sprinting effect).
    * *Settings:* FOV Boost Amount and Smoothing.

### Player Input & Camera Controller
* **Sensitivity:** Controls look speed (X and Y axis).
* **Clamps:** Vertical look limits (Default: -90 to 75).

### Physics Interaction (Push)
* **Push Power:** Base strength of the push.
* **Weight Based Push:** If true, heavier objects are harder to push.

---

## 5. Extending the System (For Programmers)

To add a new mechanic (e.g., "Slide"), follow this workflow:

### Step 1: Add the Input
1.  Open `AZE/PlayerMovementFPS/Inputs/InputSystem_Actions`.
2.  Add a new Action (e.g., "Slide") and assign a binding.
3.  In `PlayerInputHandler.cs`:
    ```csharp
    [SerializeField] private InputActionReference slideAction;
    
    // Create a property to read the state
    public bool SlideTriggered { get; private set; }

    // IN OnEnable():
    slideAction.action.Enable();

    // In Update():
    SlideTriggered = slideAction.action.WasPressedThisFrame();
    ```

### Step 2: Create the State
1.  Create `PlayerSlideState.cs` inheriting from `PlayerBaseState`.
2.  Override `EnterState`, `UpdateState`, etc.
3.  Register it in `PlayerStateFactory.cs`:
    ```csharp
    public PlayerBaseState Slide { get; private set; }
    
    // In Constructor:
    Slide = new PlayerSlideState(_context, this);
    ```

### Step 3: Connect Logic
In `PlayerWalkState` (or Sprint), add the transition logic:
```csharp
public override void CheckSwitchStates() {
    if (ctx.InputHandler.SlideTriggered) {
        SwitchState(factory.Slide);
    }
}
```

---

## 6. Support

For bug reports, feedback, or questions regarding this asset, please contact:
**[studios.aze.contato@gmail.com]**