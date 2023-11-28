using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerCam : MonoBehaviour
{
    // Mouse sensitivity for cam
    public float sensX, sensY;
    // Mouse input rotation
    private float xRot, yRot;
    // The direction the player is facing
    public Transform orientation;
    
    void Start()
    {
        // Lock the cursor in place and make it invisible
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
    
    void Update()
    {
        // Get mouse input
        xRot -= Input.GetAxisRaw("Mouse Y") * sensX;
        yRot += Input.GetAxisRaw("Mouse X") * sensY;
        
        // Prevent player from breaking their neck
        xRot = Mathf.Clamp(xRot, -90, 90);

        // Rotate the camera and player according to mouse input
        transform.rotation = Quaternion.Euler(xRot, yRot, 0);
        orientation.rotation = Quaternion.Euler(0, yRot, 0);
    }
}
