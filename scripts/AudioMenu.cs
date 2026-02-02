using Godot;
using System;

namespace PitchGame
{
    public partial class AudioMenu : Control
    {
        [Export] public OptionButton DeviceSelector;

        public override void _Ready()
        {
            if (DeviceSelector == null)
            {
                DeviceSelector = GetNodeOrNull<OptionButton>("VBoxContainer/OptionButton");
            }

            if (DeviceSelector != null)
            {
                PopulateDevices();
                DeviceSelector.ItemSelected += OnDeviceSelected;
            }
            
            GD.Print($"PitchDetector: Current Input Device: {AudioServer.InputDevice}");
            GD.Print($"PitchDetector: Available Devices: {string.Join(", ", AudioServer.GetInputDeviceList())}");
        }

        private void PopulateDevices()
        {
            DeviceSelector.Clear();
            string[] devices = AudioServer.GetInputDeviceList();
            string currentDevice = AudioServer.InputDevice;

            int selectedIdx = 0;
            for (int i = 0; i < devices.Length; i++)
            {
                DeviceSelector.AddItem(devices[i]);
                if (devices[i] == currentDevice)
                {
                    selectedIdx = i;
                }
            }

            DeviceSelector.Select(selectedIdx);
        }

        private void OnDeviceSelected(long index)
        {
            string deviceName = DeviceSelector.GetItemText((int)index);
            AudioServer.InputDevice = deviceName;
            GD.Print($"PitchDetector: Switched Input Device to: {deviceName}");
        }
    }
}
