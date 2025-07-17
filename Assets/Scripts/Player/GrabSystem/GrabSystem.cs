using UnityEngine;

public class GrabSystem : MonoBehaviour
{
    public GrabHandAttractor leftHand;
    public GrabHandAttractor rightHand;
    public float throwStrength = 5f;

    private IGrabbable leftHeld;
    private IGrabbable rightHeld;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryGrab(leftHand, ref leftHeld);
            TryGrab(rightHand, ref rightHeld);
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            Release(leftHand, ref leftHeld);
            Release(rightHand, ref rightHeld);
        }
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
