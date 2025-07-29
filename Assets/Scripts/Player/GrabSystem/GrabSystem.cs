using UnityEngine;

public class GrabSystem : MonoBehaviour
{
    public GrabHandAttractor leftHand;
    public GrabHandAttractor rightHand;
    public float throwStrength = 5f;

    [SerializeField] private MonoBehaviour inputSource;
    private IPlayerInput input;

    private IGrabbable leftHeld;
    private IGrabbable rightHeld;

    private void Awake()
    {
        input = inputSource as IPlayerInput;
        if (input == null)
        {
            Debug.LogError("GrabSystem: inputSource does not implement IPlayerInput");
        }
    }

    private void Update()
    {
        if (input == null) return;

        // LEFT HAND
        if (input.LeftGrabDown)
        {
            if (leftHeld == null)
                TryGrab(leftHand, ref leftHeld);
        }
        else if (input.LeftGrabUp)
        {
            if (leftHeld != null)
                Release(leftHand, ref leftHeld);
        }

        if (leftHeld != null && input.LeftGrabHeld)
            leftHeld.OnAttract(leftHand.transform.position);

        // RIGHT HAND (same pattern)
        if (input.RightGrabDown)
        {
            if (rightHeld == null)
                TryGrab(rightHand, ref rightHeld);
        }
        else if (input.RightGrabUp)
        {
            if (rightHeld != null)
                Release(rightHand, ref rightHeld);
        }

        if (rightHeld != null && input.RightGrabHeld)
            rightHeld.OnAttract(rightHand.transform.position);
    }

    private void TryGrab(GrabHandAttractor hand, ref IGrabbable held)
    {
        if (hand == null || held != null) return;
        IGrabbable obj = hand.DetectGrabbable();
        if (obj != null && obj.CanBeGrabbed())
        {
            obj.OnGrab(hand.transform);

            // Badges attach to the player's body and should not remain in hand
            if (obj is SecurityBadgePickup)
            {
                held = null;
            }
            else
            {
                held = obj;
            }
        }
    }

    private void Release(GrabHandAttractor hand, ref IGrabbable held)
    {
        if (hand == null || held == null) return;
        Vector2 throwForce = (Vector2)(hand.transform.right) * throwStrength;
        held.OnRelease(throwForce);
        held = null;
    }
}
