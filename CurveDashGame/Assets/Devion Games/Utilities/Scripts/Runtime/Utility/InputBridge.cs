using UnityEngine;

namespace DevionGames
{
    public static class Input
    {
        public static bool GetKeyDown(KeyCode key)
        {
            return UnityTools.GetKeyDown(key);
        }

        public static bool GetKey(KeyCode key)
        {
            return UnityTools.GetKey(key);
        }

        public static bool GetKeyUp(KeyCode key)
        {
            return UnityTools.GetKeyUp(key);
        }

        public static bool GetMouseButtonDown(int button)
        {
            return UnityTools.GetMouseButtonDown(button);
        }

        public static bool GetMouseButton(int button)
        {
            return UnityTools.GetMouseButton(button);
        }

        public static bool GetMouseButtonUp(int button)
        {
            return UnityTools.GetMouseButtonUp(button);
        }

        public static float GetAxis(string axisName)
        {
            return UnityTools.GetAxis(axisName);
        }

        public static float GetAxisRaw(string axisName)
        {
            return UnityTools.GetAxisRaw(axisName);
        }

        public static bool GetButtonDown(string buttonName)
        {
            return UnityTools.GetButtonDown(buttonName);
        }

        public static bool GetButton(string buttonName)
        {
            return UnityTools.GetButton(buttonName);
        }

        public static bool GetButtonUp(string buttonName)
        {
            return UnityTools.GetButtonUp(buttonName);
        }

        public static Vector3 mousePosition
        {
            get
            {
                return UnityTools.mousePosition;
            }
        }
    }
}
