using UnityEngine;
using System;

public class TargetAim : MonoBehaviour
{
    private Camera mainCam;
    private Vector3 mousePos;
    public GameObject bullet;
    public Transform targetAimTransform;
    public bool canFire;
    private float timer;
    public float timeBetweenFiring;

    void Start()
    {
        mainCam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
    }

    void Update()
    {
        // Get mouse position in world space
        mousePos = mainCam.ScreenToWorldPoint(Input.mousePosition);

        // Calculate direction and rotation
        Vector3 rotation = mousePos - transform.position;
        float rotZ = Mathf.Atan2(rotation.y, rotation.x) * Mathf.Rad2Deg;

        // Apply rotation to the object
        transform.rotation = Quaternion.Euler(0, 0, rotZ);

        // Handle firing cooldown
        if (!canFire)
        {
            timer += Time.deltaTime;
            if (timer > timeBetweenFiring)
            {
                canFire = true;
                timer = 0;
            }
        }

        // Fire bullet and invoke event
        if (Input.GetMouseButton(0) && canFire)
        {
            canFire = false;
            // Instantiate(bullet, targetAimTransform.position, Quaternion.identity);

            Debug.Log($"Aiming at position: {mousePos}, Rotation: {rotZ}°");
        }
    }
}
