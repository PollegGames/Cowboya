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

        if (input.LeftGrabPressed)
        {
            if (leftHeld == null)
                TryGrab(leftHand, ref leftHeld);
            else
                Release(leftHand, ref leftHeld);
        }

        if (input.RightGrabPressed)
        {
            if (rightHeld == null)
                TryGrab(rightHand, ref rightHeld);
            else
                Release(rightHand, ref rightHeld);
        }

        if (leftHeld != null)
            leftHeld.OnAttract(leftHand.transform.position);

        if (rightHeld != null)
            rightHeld.OnAttract(rightHand.transform.position);
    }

    private void TryGrab(GrabHandAttractor hand, ref IGrabbable held)
    {
        if (hand == null || held != null) return;
        IGrabbable obj = hand.DetectGrabbable();
        if (obj != null && obj.CanBeGrabbed())
        {
            obj.OnGrab(hand.transform);
            held = obj;
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
