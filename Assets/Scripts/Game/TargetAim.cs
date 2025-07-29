using UnityEngine;
using System;
using UnityEngine.InputSystem;

public class TargetAim : MonoBehaviour
{
    private Camera mainCam;
    private Vector3 mousePos;
    public GameObject bullet;
    public Transform targetAimTransform;
    public bool canFire;
    private float timer;
    public float timeBetweenFiring;

    private InputSystem_Actions controls;
    private bool attackHeld;

    private void Awake()
    {
        controls = new InputSystem_Actions();
        controls.Player.Attack.started += _ => attackHeld = true;
        controls.Player.Attack.canceled += _ => attackHeld = false;
    }

    void Start()
    {
        mainCam = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();

    void Update()
    {
        // Get mouse position in world space
        mousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());

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
        if (attackHeld && canFire)
        {
            canFire = false;
            // Instantiate(bullet, targetAimTransform.position, Quaternion.identity);

            Debug.Log($"Aiming at position: {mousePos}, Rotation: {rotZ}°");
        }
    }
}
