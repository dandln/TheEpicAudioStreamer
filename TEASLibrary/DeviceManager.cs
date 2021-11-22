using NAudio.CoreAudioApi;

namespace TEASLibrary
{
    /// <summary>
    /// Helper class to manage the list of audio output devices available to the bot
    /// </summary>
    public class DeviceManager
    {
        /// <summary>
        /// List of audio output devices available to the system.
        /// </summary>
        public List<MMDevice> OutputDevicesList { get; private set; }

        /// <summary>
        /// Initialises the class.
        /// </summary>
        public DeviceManager()
        {
            OutputDevicesList = new List<MMDevice>();
            UpdateOutputDeviceList();
        }

        /// <summary>
        /// Updates the list of available audio output devices and returns it.
        /// </summary>
        /// <returns>A list of available audio output devices.</returns>
        public List<MMDevice> UpdateOutputDeviceList()
        {
            OutputDevicesList.Clear();
            var deviceEnumerator = new MMDeviceEnumerator();

            // Iterate through available output devices and add them to the list.
            foreach (MMDevice device in deviceEnumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
                OutputDevicesList.Add(device);

            return OutputDevicesList;
        }

        /// <summary>
        /// Searches for a friendly device name in the list of audio output devices.
        /// </summary>
        /// <param name="deviceFriendlyName">The friendly device name to search for.</param>
        /// <returns>The MMDevice object whose friendly name matches, or null if it is not in the list.</returns>
        public MMDevice? FindOutputDeviceByDeviceFriendlyName(string deviceFriendlyName)
        {
            foreach (MMDevice device in OutputDevicesList)
            {
                if (device.DeviceFriendlyName == deviceFriendlyName)
                    return device;
            }
            return null;
        }
    }
}
