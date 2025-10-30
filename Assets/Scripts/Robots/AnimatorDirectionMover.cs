using UnityEngine;

namespace CowBoya.Robots
{
    /// <summary>
    /// Moves a target transform horizontally based on animator direction and speed parameters.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AnimatorDirectionMover : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform target;
        [SerializeField] private string directionParameter = "Direction";
        [SerializeField] private string speedParameter = "Speed";
        [SerializeField] private string walkBoolParameter = "IsWalking";
        [SerializeField] private float baseSpeed = 2f;
        [SerializeField] private float directionDeadZone = 0.1f;
        [SerializeField] private bool useAnimatorSpeed = true;

        private Rigidbody2D targetBody2D;
        private Rigidbody targetBody3D;
        private float currentHorizontalSpeed;
        private bool directionParameterFound;
        private bool speedParameterFound;
        private bool walkBoolParameterFound;

        private void Reset()
        {
            animator = GetComponent<Animator>();
            target = transform;
            CachePhysicsComponents();
        }

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            if (target == null)
            {
                target = transform;
            }

            CachePhysicsComponents();
            CacheAnimatorParameters();
        }

        private void OnValidate()
        {
            baseSpeed = Mathf.Max(0f, baseSpeed);
            directionDeadZone = Mathf.Max(0f, directionDeadZone);

            if (target == null)
            {
                target = transform;
            }

            if (animator == null)
            {
                animator = GetComponent<Animator>();
            }

            CachePhysicsComponents();
            CacheAnimatorParameters();
        }

        private void Update()
        {
            UpdateHorizontalSpeed();

            if (targetBody2D == null && targetBody3D == null)
            {
                ApplyTransformMovement(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            if (Mathf.Approximately(currentHorizontalSpeed, 0f))
            {
                return;
            }

            if (targetBody2D != null)
            {
                Vector2 delta = new Vector2(currentHorizontalSpeed * Time.fixedDeltaTime, 0f);
                targetBody2D.MovePosition(targetBody2D.position + delta);
            }
            else if (targetBody3D != null)
            {
                Vector3 delta = Vector3.right * (currentHorizontalSpeed * Time.fixedDeltaTime);
                targetBody3D.MovePosition(targetBody3D.position + delta);
            }
        }

        private void UpdateHorizontalSpeed()
        {
            if (animator == null)
            {
                currentHorizontalSpeed = 0f;
                return;
            }

            if (string.IsNullOrEmpty(directionParameter))
            {
                currentHorizontalSpeed = 0f;
                return;
            }

            if (!directionParameterFound)
            {
                CacheAnimatorParameters();
                if (!directionParameterFound)
                {
                    currentHorizontalSpeed = 0f;
                    return;
                }
            }

            float directionValue = animator.GetFloat(directionParameter);
            if (Mathf.Abs(directionValue) < directionDeadZone)
            {
                currentHorizontalSpeed = 0f;
                return;
            }

            if (!string.IsNullOrEmpty(walkBoolParameter))
            {
                if (!walkBoolParameterFound)
                {
                    CacheAnimatorParameters();
                }

                if (walkBoolParameterFound && !animator.GetBool(walkBoolParameter))
                {
                    currentHorizontalSpeed = 0f;
                    return;
                }
            }

            float directionSign = Mathf.Sign(directionValue);
            float speedMultiplier = 1f;
            if (useAnimatorSpeed && !string.IsNullOrEmpty(speedParameter))
            {
                if (!speedParameterFound)
                {
                    CacheAnimatorParameters();
                }

                if (speedParameterFound)
                {
                    speedMultiplier = Mathf.Max(0f, animator.GetFloat(speedParameter));
                }
            }

            currentHorizontalSpeed = directionSign * baseSpeed * speedMultiplier;
        }

        private void ApplyTransformMovement(float deltaTime)
        {
            if (target == null || Mathf.Approximately(currentHorizontalSpeed, 0f))
            {
                return;
            }

            Vector3 delta = Vector3.right * (currentHorizontalSpeed * deltaTime);
            target.position += delta;
        }

        private void CachePhysicsComponents()
        {
            if (target == null)
            {
                targetBody2D = null;
                targetBody3D = null;
                return;
            }

            targetBody2D = target.GetComponent<Rigidbody2D>();
            targetBody3D = targetBody2D == null ? target.GetComponent<Rigidbody>() : null;
        }

        private void CacheAnimatorParameters()
        {
            if (animator == null)
            {
                directionParameterFound = false;
                speedParameterFound = false;
                walkBoolParameterFound = false;
                return;
            }

            directionParameterFound = HasParameter(directionParameter, AnimatorControllerParameterType.Float);
            speedParameterFound = HasParameter(speedParameter, AnimatorControllerParameterType.Float);
            walkBoolParameterFound = HasParameter(walkBoolParameter, AnimatorControllerParameterType.Bool);
        }

        private bool HasParameter(string parameterName, AnimatorControllerParameterType type)
        {
            if (string.IsNullOrEmpty(parameterName) || animator == null)
            {
                return false;
            }

            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.name == parameterName && parameter.type == type)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
