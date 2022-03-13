/*
 * Author: Pedro José Pérez García, 756642
 * Date: 24-02-2022 (last revision)
 * Comms: Trabajo de fin de grado de Ingeniería Informática, Graphics and Imaging Lab, Universidad de Zaragoza
 *          Script to have a working camera to record scanpaths using the mouse and keyboard in case that a VR HMD isn't available.
 */
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class CameraController : MonoBehaviour
{
    public bool mouse_NoKeyboard = false;

    const float acc = 0.1f;
    float speed = 0.0f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKey(KeyCode.Space))
        {
            mouse_NoKeyboard = true;
        }
        else
        {
            mouse_NoKeyboard = false;
        }

        if(mouse_NoKeyboard)
        {
            Camera mycam = GetComponent<Camera>();
            
            float sensitivity = 0.005f;
            Vector3 vp = mycam.ScreenToViewportPoint(new Vector3(Input.mousePosition.x, Input.mousePosition.y, mycam.nearClipPlane));
            vp.x -= 0.5f;
            vp.y -= 0.5f;
            vp.x *= sensitivity;
            vp.y *= sensitivity;
            vp.x += 0.5f;
            vp.y += 0.5f;
            Vector3 sp = mycam.ViewportToScreenPoint(vp);

            Vector3 v = mycam.ScreenToWorldPoint(sp);
            transform.LookAt(v, Vector3.up);

        }
        else
        {
            var direction = new Vector3(-Input.GetAxis("Vertical"), Input.GetAxis("Horizontal"), 0.0f);
            if (Input.GetAxis("Vertical") != 0 || Input.GetAxis("Horizontal") != 0)
            {
                speed = speed + acc;
            }
            else
            {
                speed = 0.0f;
            }
            if (speed > 250.0f)
            {
                speed = 250.0f;
            }
            transform.Rotate(direction * speed * Time.deltaTime);
        }
    }
}
