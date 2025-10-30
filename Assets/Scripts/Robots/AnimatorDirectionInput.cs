using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;
#endif

namespace CowBoya.Robots
{
    /// <summary>
    /// Updates animator parameters from player horizontal input, supporting both input backends.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimatorDirectionInput : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string directionParameter = "Direction";
        [SerializeField] private string walkBoolParameter = "IsWalking";
        [SerializeField] private string speedParameter = "Speed";
#if ENABLE_LEGACY_INPUT_MANAGER
        [SerializeField] private bool useRawLegacyAxis = true;
#endif
#if ENABLE_INPUT_SYSTEM
        [SerializeField] private InputActionReference horizontalAction;
        private InputAction cachedHorizontalAction;
#endif

        private void Reset()
        {
            animator = GetComponent<Animator>();
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }
        }

        private void OnEnable()
        {
#if ENABLE_INPUT_SYSTEM
            TryInitializeAction();
            cachedHorizontalAction?.Enable();
#endif
            ResetAnimatorMovement();
        }

        private void OnDisable()
        {
#if ENABLE_INPUT_SYSTEM
            cachedHorizontalAction?.Disable();
#endif
            ResetAnimatorMovement();
        }
#if ENABLE_INPUT_SYSTEM

        private void TryInitializeAction()
        {
            if (horizontalAction == null)
            {
                cachedHorizontalAction = null;
                return;
            }

            cachedHorizontalAction = horizontalAction.action;
            if (cachedHorizontalAction == null && horizontalAction.asset != null)
            {
                cachedHorizontalAction = horizontalAction.asset.FindAction(horizontalAction.name, throwIfNotFound: false);
            }
        }
#endif

        private void Update()
        {
            if (animator == null)
            {
                return;
            }

            int direction = GetDigitalDirection();
            if (direction == 0)
            {
                ResetAnimatorMovement();
                return;
            }

            ApplyAnimatorValues(direction, true);

            if (!string.IsNullOrEmpty(speedParameter))
            {
                animator.SetFloat(speedParameter, 1f);
            }
        }

        private void ApplyAnimatorValues(float direction, bool isWalking)
        {
            if (!string.IsNullOrEmpty(walkBoolParameter))
            {
                animator.SetBool(walkBoolParameter, isWalking);
            }

            if (!string.IsNullOrEmpty(directionParameter))
            {
                animator.SetFloat(directionParameter, direction);
            }
        }

        private void ResetAnimatorMovement()
        {
            ApplyAnimatorValues(0f, false);

            if (!string.IsNullOrEmpty(speedParameter))
            {
                animator.SetFloat(speedParameter, 0f);
            }
        }

        private int GetDigitalDirection()
        {
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                bool leftPressed = Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed;
                bool rightPressed = Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed;

                if (leftPressed != rightPressed)
                {
                    return rightPressed ? 1 : -1;
                }
            }

            if (cachedHorizontalAction == null && horizontalAction != null)
            {
                TryInitializeAction();
                cachedHorizontalAction?.Enable();
            }

            if (cachedHorizontalAction != null)
            {
                if (cachedHorizontalAction.phase == InputActionPhase.Waiting || cachedHorizontalAction.activeControl == null)
                {
                    return 0;
                }

                if (TryReadActionValue(out float actionValue))
                {
                    if (Mathf.Abs(actionValue) > 0.5f)
                    {
                        return actionValue > 0f ? 1 : -1;
                    }
                }
            }
#endif

#if ENABLE_INPUT_SYSTEM
            ReadOnlyArray<Gamepad> gamepads = Gamepad.all;
            if (gamepads.Count > 0)
            {
                for (int i = 0; i < gamepads.Count; i++)
                {
                    Gamepad pad = gamepads[i];
                    if (pad == null)
                    {
                        continue;
                    }

                    float stick = pad.leftStick.ReadValue().x;
                    if (Mathf.Abs(stick) > 0.5f)
                    {
                        return stick > 0f ? 1 : -1;
                    }

                    float dpad = pad.dpad.ReadValue().x;
                    if (Mathf.Abs(dpad) > 0.5f)
                    {
                        return dpad > 0f ? 1 : -1;
                    }
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            bool legacyLeft = Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A);
            bool legacyRight = Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D);

            if (legacyLeft != legacyRight)
            {
                return legacyRight ? 1 : -1;
            }

            float horizontal = useRawLegacyAxis ? Input.GetAxisRaw("Horizontal") : Input.GetAxis("Horizontal");
            if (Mathf.Abs(horizontal) > 0.5f)
            {
                return horizontal > 0f ? 1 : -1;
            }
#endif

            return 0;
        }

#if ENABLE_INPUT_SYSTEM
        private bool TryReadActionValue(out float value)
        {
            value = 0f;
            if (cachedHorizontalAction == null)
            {
                return false;
            }

            try
            {
                value = cachedHorizontalAction.ReadValue<float>();
                return true;
            }
            catch (InvalidOperationException)
            {
                // Ignore and try other paths.
            }

            try
            {
                value = cachedHorizontalAction.ReadValue<Vector2>().x;
                return true;
            }
            catch (InvalidOperationException)
            {
                // Ignore and try object conversion.
            }

            object rawValue = cachedHorizontalAction.ReadValueAsObject();
            switch (rawValue)
            {
                case float floatValue:
                    value = floatValue;
                    return true;
                case Vector2 vector2Value:
                    value = vector2Value.x;
                    return true;
                case Vector3 vector3Value:
                    value = vector3Value.x;
                    return true;
                default:
                    if (rawValue != null && float.TryParse(rawValue.ToString(), out float parsed))
                    {
                        value = parsed;
                        return true;
                    }

                    return false;
            }
        }
#endif
    }
}
