using UnityEngine;

namespace SRDebugger
{
    public static class Input
    {
        public static bool GetKeyDown(KeyCode key)
        {
            #if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var systemKey = ConvertKeyCodeToKey(key);
                if (systemKey != UnityEngine.InputSystem.Key.None)
                {
                    return UnityEngine.InputSystem.Keyboard.current[systemKey].wasPressedThisFrame;
                }
            }
            return false;
            #else
            return UnityEngine.Input.GetKeyDown(key);
            #endif
        }

        public static bool GetKey(KeyCode key)
        {
            #if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var systemKey = ConvertKeyCodeToKey(key);
                if (systemKey != UnityEngine.InputSystem.Key.None)
                {
                    return UnityEngine.InputSystem.Keyboard.current[systemKey].isPressed;
                }
            }
            return false;
            #else
            return UnityEngine.Input.GetKey(key);
            #endif
        }

        public static bool GetKeyUp(KeyCode key)
        {
            #if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var systemKey = ConvertKeyCodeToKey(key);
                if (systemKey != UnityEngine.InputSystem.Key.None)
                {
                    return UnityEngine.InputSystem.Keyboard.current[systemKey].wasReleasedThisFrame;
                }
            }
            return false;
            #else
            return UnityEngine.Input.GetKeyUp(key);
            #endif
        }

        public static string inputString
        {
            get
            {
                #if ENABLE_INPUT_SYSTEM
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    string str = "";
                    var kb = UnityEngine.InputSystem.Keyboard.current;
                    if (kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame) str += "0";
                    if (kb.digit1Key.wasPressedThisFrame || kb.numpad1Key.wasPressedThisFrame) str += "1";
                    if (kb.digit2Key.wasPressedThisFrame || kb.numpad2Key.wasPressedThisFrame) str += "2";
                    if (kb.digit3Key.wasPressedThisFrame || kb.numpad3Key.wasPressedThisFrame) str += "3";
                    if (kb.digit4Key.wasPressedThisFrame || kb.numpad4Key.wasPressedThisFrame) str += "4";
                    if (kb.digit5Key.wasPressedThisFrame || kb.numpad5Key.wasPressedThisFrame) str += "5";
                    if (kb.digit6Key.wasPressedThisFrame || kb.numpad6Key.wasPressedThisFrame) str += "6";
                    if (kb.digit7Key.wasPressedThisFrame || kb.numpad7Key.wasPressedThisFrame) str += "7";
                    if (kb.digit8Key.wasPressedThisFrame || kb.numpad8Key.wasPressedThisFrame) str += "8";
                    if (kb.digit9Key.wasPressedThisFrame || kb.numpad9Key.wasPressedThisFrame) str += "9";
                    return str;
                }
                return "";
                #else
                return UnityEngine.Input.inputString;
                #endif
            }
        }

        private static UnityEngine.InputSystem.Key ConvertKeyCodeToKey(KeyCode key)
        {
            #if ENABLE_INPUT_SYSTEM
            switch (key)
            {
                case KeyCode.None: return UnityEngine.InputSystem.Key.None;
                case KeyCode.Space: return UnityEngine.InputSystem.Key.Space;
                case KeyCode.Return: return UnityEngine.InputSystem.Key.Enter;
                case KeyCode.Tab: return UnityEngine.InputSystem.Key.Tab;
                case KeyCode.Escape: return UnityEngine.InputSystem.Key.Escape;
                case KeyCode.Backspace: return UnityEngine.InputSystem.Key.Backspace;
                case KeyCode.Delete: return UnityEngine.InputSystem.Key.Delete;
                
                case KeyCode.A: return UnityEngine.InputSystem.Key.A;
                case KeyCode.B: return UnityEngine.InputSystem.Key.B;
                case KeyCode.C: return UnityEngine.InputSystem.Key.C;
                case KeyCode.D: return UnityEngine.InputSystem.Key.D;
                case KeyCode.E: return UnityEngine.InputSystem.Key.E;
                case KeyCode.F: return UnityEngine.InputSystem.Key.F;
                case KeyCode.G: return UnityEngine.InputSystem.Key.G;
                case KeyCode.H: return UnityEngine.InputSystem.Key.H;
                case KeyCode.I: return UnityEngine.InputSystem.Key.I;
                case KeyCode.J: return UnityEngine.InputSystem.Key.J;
                case KeyCode.K: return UnityEngine.InputSystem.Key.K;
                case KeyCode.L: return UnityEngine.InputSystem.Key.L;
                case KeyCode.M: return UnityEngine.InputSystem.Key.M;
                case KeyCode.N: return UnityEngine.InputSystem.Key.N;
                case KeyCode.O: return UnityEngine.InputSystem.Key.O;
                case KeyCode.P: return UnityEngine.InputSystem.Key.P;
                case KeyCode.Q: return UnityEngine.InputSystem.Key.Q;
                case KeyCode.R: return UnityEngine.InputSystem.Key.R;
                case KeyCode.S: return UnityEngine.InputSystem.Key.S;
                case KeyCode.T: return UnityEngine.InputSystem.Key.T;
                case KeyCode.U: return UnityEngine.InputSystem.Key.U;
                case KeyCode.V: return UnityEngine.InputSystem.Key.V;
                case KeyCode.W: return UnityEngine.InputSystem.Key.W;
                case KeyCode.X: return UnityEngine.InputSystem.Key.X;
                case KeyCode.Y: return UnityEngine.InputSystem.Key.Y;
                case KeyCode.Z: return UnityEngine.InputSystem.Key.Z;
                
                case KeyCode.Alpha0: return UnityEngine.InputSystem.Key.Digit0;
                case KeyCode.Alpha1: return UnityEngine.InputSystem.Key.Digit1;
                case KeyCode.Alpha2: return UnityEngine.InputSystem.Key.Digit2;
                case KeyCode.Alpha3: return UnityEngine.InputSystem.Key.Digit3;
                case KeyCode.Alpha4: return UnityEngine.InputSystem.Key.Digit4;
                case KeyCode.Alpha5: return UnityEngine.InputSystem.Key.Digit5;
                case KeyCode.Alpha6: return UnityEngine.InputSystem.Key.Digit6;
                case KeyCode.Alpha7: return UnityEngine.InputSystem.Key.Digit7;
                case KeyCode.Alpha8: return UnityEngine.InputSystem.Key.Digit8;
                case KeyCode.Alpha9: return UnityEngine.InputSystem.Key.Digit9;
                
                case KeyCode.Keypad0: return UnityEngine.InputSystem.Key.Numpad0;
                case KeyCode.Keypad1: return UnityEngine.InputSystem.Key.Numpad1;
                case KeyCode.Keypad2: return UnityEngine.InputSystem.Key.Numpad2;
                case KeyCode.Keypad3: return UnityEngine.InputSystem.Key.Numpad3;
                case KeyCode.Keypad4: return UnityEngine.InputSystem.Key.Numpad4;
                case KeyCode.Keypad5: return UnityEngine.InputSystem.Key.Numpad5;
                case KeyCode.Keypad6: return UnityEngine.InputSystem.Key.Numpad6;
                case KeyCode.Keypad7: return UnityEngine.InputSystem.Key.Numpad7;
                case KeyCode.Keypad8: return UnityEngine.InputSystem.Key.Numpad8;
                case KeyCode.Keypad9: return UnityEngine.InputSystem.Key.Numpad9;
                
                case KeyCode.UpArrow: return UnityEngine.InputSystem.Key.UpArrow;
                case KeyCode.DownArrow: return UnityEngine.InputSystem.Key.DownArrow;
                case KeyCode.LeftArrow: return UnityEngine.InputSystem.Key.LeftArrow;
                case KeyCode.RightArrow: return UnityEngine.InputSystem.Key.RightArrow;
                
                case KeyCode.LeftShift: return UnityEngine.InputSystem.Key.LeftShift;
                case KeyCode.RightShift: return UnityEngine.InputSystem.Key.RightShift;
                case KeyCode.LeftControl: return UnityEngine.InputSystem.Key.LeftCtrl;
                case KeyCode.RightControl: return UnityEngine.InputSystem.Key.RightCtrl;
                case KeyCode.LeftAlt: return UnityEngine.InputSystem.Key.LeftAlt;
                case KeyCode.RightAlt: return UnityEngine.InputSystem.Key.RightAlt;
                
                case KeyCode.F1: return UnityEngine.InputSystem.Key.F1;
                case KeyCode.F2: return UnityEngine.InputSystem.Key.F2;
                case KeyCode.F3: return UnityEngine.InputSystem.Key.F3;
                case KeyCode.F4: return UnityEngine.InputSystem.Key.F4;
                case KeyCode.F5: return UnityEngine.InputSystem.Key.F5;
                case KeyCode.F6: return UnityEngine.InputSystem.Key.F6;
                case KeyCode.F7: return UnityEngine.InputSystem.Key.F7;
                case KeyCode.F8: return UnityEngine.InputSystem.Key.F8;
                case KeyCode.F9: return UnityEngine.InputSystem.Key.F9;
                case KeyCode.F10: return UnityEngine.InputSystem.Key.F10;
                case KeyCode.F11: return UnityEngine.InputSystem.Key.F11;
                case KeyCode.F12: return UnityEngine.InputSystem.Key.F12;
                
                default:
                    try {
                        return (UnityEngine.InputSystem.Key)System.Enum.Parse(typeof(UnityEngine.InputSystem.Key), key.ToString(), true);
                    } catch {
                        return UnityEngine.InputSystem.Key.None;
                    }
            }
            #else
            return UnityEngine.InputSystem.Key.None;
            #endif
        }
    }
}
