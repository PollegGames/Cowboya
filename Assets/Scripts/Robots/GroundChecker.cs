using UnityEngine;

public class GroundChecker : MonoBehaviour
{
    private int grounded = 0;

    public bool IsGrounded => grounded > 0;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        grounded++;
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        grounded = Mathf.Max(grounded - 1, 0);
    }


}
