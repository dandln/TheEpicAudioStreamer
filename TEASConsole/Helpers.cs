using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using Serilog;
using TEASLibrary;

namespace TEASConsole
{
    /// <summary>
    /// Provides static helper functions for TEASConsole
    /// </summary>
    internal static class Helpers
    {
        /// <summary>
        /// Checks whether an update for TEASConsole is available by looking at the latest release on GitHub
        /// </summary>
        /// <param name="appVersion">The application version to compare to the newest version</param>
        /// <returns>1 if a newer version is available, 0 if no newer version is available, -1 if update check resulted in an error</returns>
        public static int CheckUpdate(Version appVersion)
        {
            string latestReleaseJson = "";
            try
            {
                // Download latest release information from GitHub
                using HttpClient client = new();
                client.DefaultRequestHeaders.Add("User-Agent", "TEASConsole Update Checker");
                latestReleaseJson = client.GetStringAsync("https://api.github.com/repos/dandln/theepicaudiostreamer/releases/latest").Result;
            }
            catch (Exception)
            {
                return -1;  // Update check failed
            }

            // Search for latest version
            var match = new Regex(@"""tag_name"":""v(.+?)""").Match(latestReleaseJson);

            // Version number not found
            if (match.Success == false)
                return -1;

            string latestVersion = match.Groups[1].Value;

            // Compare latest version to application
            var comparison = appVersion.CompareTo(new Version(latestVersion));
            if (comparison < 0)
                return 1;   // Newer version is available
            else
                return 0;   // No newer version available
        }

        /// <summary>
        /// Prompts the user to select an audio playback device in the command console
        /// </summary>
        /// <param name="deviceName">Optional: The friendly device name already preselected by the user</param>
        /// <returns>The audio device, or null if no valid device was selected</returns>
        public static MMDevice SelectDevice(string deviceName = "")
        {
            DeviceManager devMgr = new();
            List<MMDevice> devicesList = devMgr.OutputDevicesList;
            MMDevice audioDevice;
            int userDeviceID;

            // If a preselected device name is given, use this device
            if (deviceName != "")
            {
                audioDevice = devMgr.FindOutputDeviceByDeviceFriendlyName(deviceName);

                if (audioDevice != null)
                {
                    Log.Information("The device \"{0}\" is being used in this session as per command line argument", audioDevice.DeviceFriendlyName);
                    return audioDevice;
                }

                // A device name was given, but it is invalid
                Log.Warning("\"{0}\" was given as a device name via command line argument, but it either does not exist or is unavailable", deviceName);
            }

            // Prompt user to select a readable device ID
            Console.WriteLine("\nPlease type the ID of the audio device you want to stream from:");
            foreach (MMDevice device in devicesList)
            {
                Console.WriteLine($"[{devicesList.IndexOf(device)}] {device.DeviceFriendlyName} - {device.FriendlyName}");
            }
            Console.Write("> ");
            string userInput = Console.ReadLine();

            // Parse user input and get audio device
            try
            {
                userDeviceID = int.Parse(userInput);
                audioDevice = devicesList[userDeviceID];
            }
            catch (FormatException)
            {
                Console.WriteLine("Incorrect input. Integer ID expected.");
                return null;
            }
            catch (ArgumentOutOfRangeException)
            {
                Console.WriteLine("Not a valid device ID.");
                return null;
            }

            Console.WriteLine();
            return audioDevice;
        }
    }
}
