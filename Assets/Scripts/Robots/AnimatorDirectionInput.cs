using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
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
        [SerializeField] private float inputDeadZone = 0.1f;
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

#if ENABLE_INPUT_SYSTEM
        private void OnEnable()
        {
            TryInitializeAction();
            cachedHorizontalAction?.Enable();
        }

        private void OnDisable()
        {
            cachedHorizontalAction?.Disable();
        }

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

            if (!TryGetHorizontalInput(out float horizontal))
            {
                ApplyAnimatorValues(0f, false);
                return;
            }

            bool hasInput = Mathf.Abs(horizontal) > inputDeadZone;
            float direction = hasInput ? Mathf.Sign(horizontal) : 0f;
            ApplyAnimatorValues(direction, hasInput);

            if (!string.IsNullOrEmpty(speedParameter))
            {
                animator.SetFloat(speedParameter, Mathf.Abs(horizontal));
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

        private bool TryGetHorizontalInput(out float horizontal)
        {
            horizontal = 0f;
#if ENABLE_INPUT_SYSTEM
            if (cachedHorizontalAction == null && horizontalAction != null)
            {
                TryInitializeAction();
                cachedHorizontalAction?.Enable();
            }

            if (cachedHorizontalAction != null)
            {
                try
                {
                    horizontal = cachedHorizontalAction.ReadValue<float>();
                    return true;
                }
                catch (InvalidOperationException)
                {
                    // Fall back to vector reads below.
                }

                try
                {
                    horizontal = cachedHorizontalAction.ReadValue<Vector2>().x;
                    return true;
                }
                catch (InvalidOperationException)
                {
                    // Fall back to object conversion below.
                }

                object value = cachedHorizontalAction.ReadValueAsObject();
                switch (value)
                {
                    case float floatValue:
                        horizontal = floatValue;
                        return true;
                    case Vector2 vector2Value:
                        horizontal = vector2Value.x;
                        return true;
                    case Vector3 vector3Value:
                        horizontal = vector3Value.x;
                        return true;
                    default:
                        if (value != null && float.TryParse(value.ToString(), out float parsed))
                        {
                            horizontal = parsed;
                            return true;
                        }
                        break;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            horizontal = useRawLegacyAxis ? Input.GetAxisRaw("Horizontal") : Input.GetAxis("Horizontal");
            return true;
#else
            return false;
#endif
        }
    }
}
