using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class XRTriggerHaptics : MonoBehaviour
{
    private const float TriggerThreshold = 0.75f;
    private const float TriggerPulseAmplitude = 0.6f;
    private const float TriggerPulseDuration = 0.1f;

    private static XRTriggerHaptics instance;

    private readonly List<InputDevice> devices = new List<InputDevice>();
    private bool leftTriggerWasPressed;
    private bool rightTriggerWasPressed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureInstance()
    {
        if (instance != null)
            return;

        GameObject hapticsObject = new GameObject("XR Trigger Haptics");
        DontDestroyOnLoad(hapticsObject);
        instance = hapticsObject.AddComponent<XRTriggerHaptics>();
    }

    private void Update()
    {
        UpdateHand(XRNode.LeftHand, ref leftTriggerWasPressed);
        UpdateHand(XRNode.RightHand, ref rightTriggerWasPressed);
    }

    private void UpdateHand(XRNode handNode, ref bool wasPressed)
    {
        InputDevice device = InputDevices.GetDeviceAtXRNode(handNode);
        if (!device.isValid || !device.TryGetFeatureValue(CommonUsages.trigger, out float triggerValue))
        {
            wasPressed = false;
            return;
        }

        bool isPressed = triggerValue >= TriggerThreshold;
        if (isPressed && !wasPressed)
            SendPulse(device);

        wasPressed = isPressed;
    }

    private void SendPulse(InputDevice preferredDevice)
    {
        if (preferredDevice.TryGetHapticCapabilities(out HapticCapabilities capabilities)
            && capabilities.supportsImpulse)
        {
            preferredDevice.SendHapticImpulse(0u, TriggerPulseAmplitude, TriggerPulseDuration);
            return;
        }

        devices.Clear();
        InputDevices.GetDevicesWithCharacteristics(
            InputDeviceCharacteristics.Controller | InputDeviceCharacteristics.HeldInHand,
            devices);

        for (int i = 0; i < devices.Count; i++)
        {
            InputDevice fallbackDevice = devices[i];
            if (!fallbackDevice.isValid)
                continue;

            if (fallbackDevice.TryGetHapticCapabilities(out capabilities) && capabilities.supportsImpulse)
            {
                fallbackDevice.SendHapticImpulse(0u, TriggerPulseAmplitude, TriggerPulseDuration);
                return;
            }
        }
    }
}
