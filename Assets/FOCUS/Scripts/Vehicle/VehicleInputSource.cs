using UnityEngine;
using UnityEngine.InputSystem;

namespace FocusSim.Vehicle
{
    public readonly struct VehicleInputState
    {
        public readonly float Steering;
        public readonly float Throttle;
        public readonly float Brake;
        public readonly bool Handbrake;

        public VehicleInputState(float steering, float throttle, float brake, bool handbrake)
        {
            Steering = Mathf.Clamp(steering, -1f, 1f);
            Throttle = Mathf.Clamp01(throttle);
            Brake = Mathf.Clamp01(brake);
            Handbrake = handbrake;
        }
    }

    public sealed class VehicleInputSource : MonoBehaviour
    {
        public VehicleInputState CurrentInput { get; private set; }

        private void Update()
        {
            float steering = 0f;
            float throttle = 0f;
            float brake = 0f;
            bool handbrake = false;

#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null)
            {
                steering += Keyboard.current.aKey.isPressed ? -1f : 0f;
                steering += Keyboard.current.dKey.isPressed ? 1f : 0f;
                throttle = Keyboard.current.wKey.isPressed ? 1f : 0f;
                brake = Keyboard.current.sKey.isPressed ? 1f : 0f;
                handbrake = Keyboard.current.spaceKey.isPressed;
            }

            if (Gamepad.current != null)
            {
                steering = Mathf.Abs(Gamepad.current.leftStick.ReadValue().x) > Mathf.Abs(steering)
                    ? Gamepad.current.leftStick.ReadValue().x
                    : steering;
                throttle = Mathf.Max(throttle, Gamepad.current.rightTrigger.ReadValue());
                brake = Mathf.Max(brake, Gamepad.current.leftTrigger.ReadValue());
                handbrake |= Gamepad.current.buttonSouth.isPressed;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            steering += Input.GetKey(KeyCode.A) ? -1f : 0f;
            steering += Input.GetKey(KeyCode.D) ? 1f : 0f;
            throttle = Mathf.Max(throttle, Input.GetKey(KeyCode.W) ? 1f : 0f);
            brake = Mathf.Max(brake, Input.GetKey(KeyCode.S) ? 1f : 0f);
            handbrake |= Input.GetKey(KeyCode.Space);
#endif

            CurrentInput = new VehicleInputState(steering, throttle, brake, handbrake);
        }
    }
}
