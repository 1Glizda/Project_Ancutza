using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Han.CheckIn
{
    /// <summary>
    /// End-of-Night Trigger component.
    /// Can be attached to a desk bell, bed, door, trigger collider, or UI button to smoothly transition
    /// out of gameplay and load the next night's teaser screen (e.g. "Night Two, keep going") via GameLoopManager.
    /// </summary>
    public class NightCompletionTrigger : MonoBehaviour
    {
        [Header("Trigger Activation Modes")]
        [Tooltip("If true, entering a trigger collider on this GameObject with the 'Player' tag completes the night.")]
        [SerializeField] private bool triggerOnPlayerColliderEnter = true;

        [Tooltip("If true, pressing the shortcut key during active gameplay triggers the end-of-night transition.")]
        [SerializeField] private bool allowKeyboardShortcut = true;
        [SerializeField] private KeyCode completeNightHotkey = KeyCode.F6;

        [Header("Events")]
        public UnityEvent onEndOfNightTriggered;

        private void Update()
        {
            if (!allowKeyboardShortcut) return;
            if (GameLoopManager.Instance == null || !GameLoopManager.Instance.IsGameplayActive) return;

            bool pressed = false;
            if (Keyboard.current != null && completeNightHotkey == KeyCode.F6 && Keyboard.current.f6Key.wasPressedThisFrame)
            {
                pressed = true;
            }
            if (!pressed && completeNightHotkey != KeyCode.None)
            {
                try
                {
                    if (Input.GetKeyDown(completeNightHotkey)) pressed = true;
                }
                catch { }
            }

            if (pressed)
            {
                TriggerEndOfNight();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!triggerOnPlayerColliderEnter) return;
            if (other != null && other.CompareTag("Player"))
            {
                TriggerEndOfNight();
            }
        }

        /// <summary>
        /// Public method callable from UnityEvents, UI Buttons, interactables, or scripts
        /// to smoothly end the current night and load the next Night Teaser screen.
        /// </summary>
        [ContextMenu("Trigger End of Night")]
        public void TriggerEndOfNight()
        {
            if (GameLoopManager.Instance == null)
            {
                Debug.LogWarning("[NightCompletionTrigger] No GameLoopManager found in scene.");
                return;
            }

            onEndOfNightTriggered?.Invoke();
            GameLoopManager.Instance.CompleteCurrentNight();
        }
    }
}
