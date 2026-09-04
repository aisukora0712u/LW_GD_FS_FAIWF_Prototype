using System;
using Game.Foundation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Infrastructure
{
    [DisallowMultipleComponent]
    public sealed class PlayerInputService : MonoBehaviour, IInputService
    {
        [SerializeField] private PlayerInput playerInput;

        public event Action<string> ControlSchemeChanged;
        public string CurrentControlScheme => playerInput == null ? string.Empty : playerInput.currentControlScheme;

        private void Awake()
        {
            if (playerInput == null)
            {
                playerInput = GetComponent<PlayerInput>();
            }
        }

        private void OnEnable()
        {
            if (playerInput != null)
            {
                playerInput.onControlsChanged += HandleControlsChanged;
            }
        }

        private void OnDisable()
        {
            if (playerInput != null)
            {
                playerInput.onControlsChanged -= HandleControlsChanged;
            }
        }

        public void SetGameplayEnabled(bool enabled)
        {
            if (playerInput == null)
            {
                return;
            }

            if (enabled)
            {
                playerInput.ActivateInput();
            }
            else
            {
                playerInput.DeactivateInput();
            }
        }

        private void HandleControlsChanged(PlayerInput input) => ControlSchemeChanged?.Invoke(input.currentControlScheme);
    }
}
