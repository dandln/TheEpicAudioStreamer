using System.Text.RegularExpressions;
using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace TEASLibrary
{
    /// <summary>
    /// Provides stataic helper functions for the audio streamer bot.
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Converts an IEEE Floating Point audio buffer into a 16bit PCM compatible buffer.
        /// </summary>
        /// <param name="inputBuffer">The buffer in IEEE Floating Point format.</param>
        /// <param name="length">The number of bytes in the buffer.</param>
        /// <param name="format">The WaveFormat of the buffer.</param>
        /// <returns>A byte array that represents the given buffer converted into PCM format.</returns>
        internal static byte[] AudioToPCM16(byte[] inputBuffer, int length, WaveFormat format)
        {
            if (length == 0)
                return Array.Empty<byte>(); // No bytes recorded, return empty array.

            // Create a WaveStream from the input buffer.
            using var memStream = new MemoryStream(inputBuffer, 0, length);
            using var inputStream = new RawSourceWaveStream(memStream, format);

            // Convert the input stream to a WaveProvider in 16bit PCM format with sample rate of 48000 Hz.
            var convertedPCM = new SampleToWaveProvider16(
                new WdlResamplingSampleProvider(
                    new WaveToSampleProvider(inputStream),
                    48000)
                );

            byte[] convertedBuffer = new byte[length];

            using var stream = new MemoryStream();
            int read;

            // Read the converted WaveProvider into a buffer and turn it into a Stream.
            while ((read = convertedPCM.Read(convertedBuffer, 0, length)) > 0)
                stream.Write(convertedBuffer, 0, read);

            // Return the converted Stream as a byte array.
            return stream.ToArray();
        }

        /// <summary>
        /// Generates an embed message.
        /// </summary>
        /// <param name="color">The color of the embed as a DiscordColor object.</param>
        /// <param name="title">The title of the embed.</param>
        /// <param name="description">The description of the embed.</param>
        /// <returns>The built DiscordEmbed object.</returns>
        internal static DiscordEmbed GenerateEmbed(DiscordColor color, string title, string description)
        {
            var embed = new DiscordEmbedBuilder
            {
                Color = color,
                Title = title,
                Description = description
            };
            return embed.Build();
        }

        /// <summary>
        /// Generates an embed message.
        /// </summary>
        /// <param name="color">The color of the embed as a DiscordColor object.</param>
        /// <param name="description">The description of the embed.</param>
        /// <returns>The built DiscordEmbed object.</returns>
        internal static DiscordEmbed GenerateEmbed(DiscordColor color, string description)
        {
            var embed = new DiscordEmbedBuilder
            {
                Color = color,
                Description = description
            };

            return embed.Build();
        }

        /// <summary>
        /// Checks whether the bot is connected to a voice channel.
        /// </summary>
        /// <param name="ctx">The current command context.</param>
        /// <returns>True if connected, false if not.</returns>
        internal static bool CheckConnectionStatus(InteractionContext ctx)
        {
            var connection = ctx.Client.GetVoiceNext().GetConnection(ctx.Guild);
            if (connection == null)
                return false;
            else
                return true;
        }
    }
}
