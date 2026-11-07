using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Han.CheckIn
{
    /// <summary>
    /// Manages EventSystem focus across all menus and floating prompts for seamless
    /// Keyboard (WASD / Arrow Keys / Enter / Space) and Gamepad (D-Pad / Left Stick / South Button) navigation
    /// without mouse-drift deselection issues.
    /// </summary>
    [DisallowMultipleComponent]
    public class HanUIFocusNavigator : MonoBehaviour
    {
        public static HanUIFocusNavigator Instance { get; private set; }

        [Header("Default Focus Targets per Menu")]
        [SerializeField] private Selectable mainMenuDefaultSelectable;
        [SerializeField] private Selectable pauseMenuDefaultSelectable;
        [SerializeField] private Selectable settingsDefaultSelectable;
        [SerializeField] private Selectable creditsDefaultSelectable;
        [SerializeField] private Selectable introVideoDefaultSelectable;

        private GameObject _lastValidSelection;
        private Selectable _activePromptSelectable;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ConfigureDefaults(
            Selectable mainMenuDefault,
            Selectable pauseDefault,
            Selectable settingsDefault,
            Selectable creditsDefault,
            Selectable introVideoDefault)
        {
            mainMenuDefaultSelectable = mainMenuDefault;
            pauseMenuDefaultSelectable = pauseDefault;
            settingsDefaultSelectable = settingsDefault;
            creditsDefaultSelectable = creditsDefault;
            introVideoDefaultSelectable = introVideoDefault;
        }

        /// <summary>
        /// Registers or clears a floating world-space prompt (such as the NPC "Go on" button)
        /// as the primary focus target during active desk dialogue.
        /// </summary>
        public static void RegisterActivePrompt(Selectable promptSelectable)
        {
            if (Instance == null) return;
            Instance._activePromptSelectable = promptSelectable;
            if (promptSelectable != null && promptSelectable.gameObject.activeInHierarchy)
            {
                Instance.SelectObjectSafe(promptSelectable.gameObject);
            }
        }

        public static void ClearActivePrompt(Selectable promptSelectable)
        {
            if (Instance == null) return;
            if (Instance._activePromptSelectable == promptSelectable)
            {
                Instance._activePromptSelectable = null;
            }
        }

        /// <summary>
        /// Assigns EventSystem focus to the specified Selectable immediately.
        /// </summary>
        public void FocusSelectable(Selectable target)
        {
            if (target == null || !target.gameObject.activeInHierarchy || !target.interactable)
                return;

            SelectObjectSafe(target.gameObject);
        }

        /// <summary>
        /// Configures wrap-around explicit vertical navigation for a list of menu buttons
        /// so Up/Down arrow keys and D-Pad never get stuck or lose focus at list boundaries.
        /// </summary>
        public static void ConfigureVerticalNavigationLoop(IList<Button> buttons)
        {
            if (buttons == null || buttons.Count == 0) return;
            int count = buttons.Count;
            for (int i = 0; i < count; i++)
            {
                Button current = buttons[i];
                if (current == null) continue;

                Button prev = buttons[(i - 1 + count) % count];
                Button next = buttons[(i + 1) % count];

                Navigation nav = current.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = prev;
                nav.selectOnDown = next;
                current.navigation = nav;
            }
        }

        private void Update()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return;

            GameObject current = es.currentSelectedGameObject;
            if (current != null && current.activeInHierarchy)
            {
                var sel = current.GetComponent<Selectable>();
                if (sel != null && sel.interactable)
                {
                    _lastValidSelection = current;
                    return;
                }
            }

            // If selection was lost (e.g., clicking empty background space or panel switch)
            // and the player presses any navigation or submit input, or a menu is open, restore focus cleanly.
            if (WasNavigationOrSubmitPressed() || ShouldMaintainMenuFocus())
            {
                RecoverFocus();
            }
        }

        private bool ShouldMaintainMenuFocus()
        {
            if (GameLoopManager.Instance == null) return false;
            var state = GameLoopManager.Instance.CurrentState;
            return state == GameLoopState.MainMenu || state == GameLoopState.Paused;
        }

        public void RecoverFocus()
        {
            // 1. Check sub-panels first (Settings / Credits)
            if (settingsDefaultSelectable != null && settingsDefaultSelectable.gameObject.activeInHierarchy)
            {
                SelectObjectSafe(settingsDefaultSelectable.gameObject);
                return;
            }
            if (creditsDefaultSelectable != null && creditsDefaultSelectable.gameObject.activeInHierarchy)
            {
                SelectObjectSafe(creditsDefaultSelectable.gameObject);
                return;
            }

            // 2. Check last valid selection if still active in hierarchy
            if (_lastValidSelection != null && _lastValidSelection.activeInHierarchy)
            {
                var sel = _lastValidSelection.GetComponent<Selectable>();
                if (sel != null && sel.interactable)
                {
                    SelectObjectSafe(_lastValidSelection);
                    return;
                }
            }

            // 3. Check GameLoopState defaults
            if (GameLoopManager.Instance != null)
            {
                switch (GameLoopManager.Instance.CurrentState)
                {
                    case GameLoopState.MainMenu:
                        if (mainMenuDefaultSelectable != null && mainMenuDefaultSelectable.gameObject.activeInHierarchy)
                        {
                            SelectObjectSafe(mainMenuDefaultSelectable.gameObject);
                            return;
                        }
                        break;

                    case GameLoopState.Paused:
                        if (pauseMenuDefaultSelectable != null && pauseMenuDefaultSelectable.gameObject.activeInHierarchy)
                        {
                            SelectObjectSafe(pauseMenuDefaultSelectable.gameObject);
                            return;
                        }
                        break;

                    case GameLoopState.IntroVideo:
                        if (introVideoDefaultSelectable != null && introVideoDefaultSelectable.gameObject.activeInHierarchy)
                        {
                            SelectObjectSafe(introVideoDefaultSelectable.gameObject);
                            return;
                        }
                        break;
                }
            }

            // 4. Check floating NPC "Go on" prompt during gameplay
            if (_activePromptSelectable != null && _activePromptSelectable.gameObject.activeInHierarchy)
            {
                SelectObjectSafe(_activePromptSelectable.gameObject);
            }
        }

        private void SelectObjectSafe(GameObject target)
        {
            if (target == null || EventSystem.current == null) return;
            _lastValidSelection = target;
            if (EventSystem.current.currentSelectedGameObject != target)
            {
                EventSystem.current.SetSelectedGameObject(target);
            }
        }

        private static bool WasNavigationOrSubmitPressed()
        {
            if (Keyboard.current != null)
            {
                var kb = Keyboard.current;
                if (kb.upArrowKey.wasPressedThisFrame ||
                    kb.downArrowKey.wasPressedThisFrame ||
                    kb.leftArrowKey.wasPressedThisFrame ||
                    kb.rightArrowKey.wasPressedThisFrame ||
                    kb.wKey.wasPressedThisFrame ||
                    kb.sKey.wasPressedThisFrame ||
                    kb.aKey.wasPressedThisFrame ||
                    kb.dKey.wasPressedThisFrame ||
                    kb.tabKey.wasPressedThisFrame ||
                    kb.enterKey.wasPressedThisFrame ||
                    kb.spaceKey.wasPressedThisFrame)
                {
                    return true;
                }
            }

            if (Gamepad.current != null)
            {
                var gp = Gamepad.current;
                if (gp.dpad.up.wasPressedThisFrame ||
                    gp.dpad.down.wasPressedThisFrame ||
                    gp.dpad.left.wasPressedThisFrame ||
                    gp.dpad.right.wasPressedThisFrame ||
                    gp.leftStick.ReadValue().sqrMagnitude > 0.25f ||
                    gp.buttonSouth.wasPressedThisFrame)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
