using System.Text.RegularExpressions;
using DSharpPlus.SlashCommands;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using DSharpPlus;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;

namespace TEASLibrary
{
    /// <summary>
    /// Provides stataic helper functions for the audio streamer bot
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Converts an IEEE Floating Point audio buffer into a 16bit PCM compatible buffer
        /// </summary>
        /// <param name="inputBuffer">The buffer in IEEE Floating Point format</param>
        /// <param name="length">The number of bytes in the buffer</param>
        /// <param name="format">The WaveFormat of the buffer</param>
        /// <returns>A byte array that represents the given buffer converted into PCM format</returns>
        internal static byte[] AudioToPCM16(byte[] inputBuffer, int length, WaveFormat format)
        {
            if (length == 0)
                return Array.Empty<byte>(); // No bytes recorded, return empty array

            // Create a WaveStream from the input buffer.
            using var memStream = new MemoryStream(inputBuffer, 0, length);
            using var inputStream = new RawSourceWaveStream(memStream, format);

            // Convert the input stream to a WaveProvider in 16bit PCM format with sample rate of 48000 Hz
            var convertedPCM = new SampleToWaveProvider16(
                new WdlResamplingSampleProvider(
                    new WaveToSampleProvider(inputStream),
                    48000)
                );

            byte[] convertedBuffer = new byte[length];

            using var stream = new MemoryStream();
            int read;

            // Read the converted WaveProvider into a buffer and turn it into a Stream
            while ((read = convertedPCM.Read(convertedBuffer, 0, length)) > 0)
                stream.Write(convertedBuffer, 0, read);

            // Return the converted Stream as a byte array
            return stream.ToArray();
        }

        /// <summary>
        /// Performs feasibility checks for the execution of the command provided in the interaction context, based on the given options.
        /// This function is designed to handle both the checks as well as log output so that the calling command can just stop
        /// executing depending on the result.
        /// </summary>
        /// <param name="ctx">The InteractionContext of the command</param>
        /// <param name="botInstance">The admin username provided to the bot instance</param>
        /// <param name="checkPermissions">Check whether the user issuing the command has permissions to execute it</param>
        /// <param name="checkBotConnected">Check whether the bot is connected to a voice channel</param>
        /// <param name="checkBotNotConnected">Check whether the bot is not connected to a voice channel</param>
        /// <param name="checkUserConnected">Check whether the issuing user is connected to a voice channel</param>
        /// <param name="checkDeviceSelected">Check whether there is currently an active audio device selected</param>
        /// <param name="checkBotStreaming">Check whether the bot is currently streaming</param>
        /// <param name="checkBotNotStreaming">Check whether the bot is not currently streaming</param>
        /// <returns>True if all checks have passed, false if one of the checks has failed (i.e. returned false itself)</returns>
        public static bool CheckCommandFeasibility(
            InteractionContext ctx,
            Bot botInstance,
            bool checkPermissions = false,
            bool checkBotConnected = false,
            bool checkBotNotConnected = false,
            bool checkUserConnected = false,
            bool checkDeviceSelected = false,
            bool checkBotStreaming = false,
            bool checkBotNotStreaming = false)
        {
            var connection = ctx.Client.GetVoiceNext().GetConnection(ctx.Guild);
            var voicestate = ctx.Member?.VoiceState;

            if (checkPermissions)
            {
                // Return false if user is neither owner of the appliaction, server manager, flagged as an admin user nor has a role flagged as an admin role
                if (!ctx.Client.CurrentApplication.Owners.Contains(ctx.User) &&
                    !ctx.Member.PermissionsIn(ctx.Channel).HasFlag(DSharpPlus.Permissions.ManageGuild) &&
                    !botInstance.BotConfig.AdminUsers.Contains(ctx.Member.Username + "#" + ctx.Member.Discriminator) &&
                    !CheckIfAdminRole(ctx.Member, botInstance.BotConfig.AdminRoles))
                {
                    ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (GenerateEmbed(DiscordColor.Red, $"Sorry {ctx.Member.Mention}, you're not the DJ today")).AsEphemeral(true));
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User}#{Discriminator} - Permission denied", ctx.CommandName, ctx.Member.Username, ctx.Member.Discriminator);
                    return false;
                }
            }
            if (checkBotConnected)
            {
                // Returns false if the bot is not currently connected to a voice channel
                if (connection == null)
                {
                    ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "Bot is not connected to a voice channel")).AsEphemeral(true));
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User}#{Discriminator} - Bot not in a voice channel", ctx.CommandName, ctx.Member.Username, ctx.Member.Discriminator);
                    return false;
                }
            }
            if (checkBotNotConnected)
            {
                // Returns false if the bot is already connected to a voice channel
                if (connection != null)
                {
                    ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "Bot is already connected to a voice channel")).AsEphemeral(true));
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User}#{Discriminator} - Bot already in a voice channel", ctx.CommandName, ctx.Member.Username, ctx.Member.Discriminator);
                    return false;
                }
            }
            if (checkUserConnected)
            {
                // Returns false if the member issuing the command is not currently connected to a voice channel
                if (voicestate?.Channel == null)
                {
                    ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "You are not in a voice channel")).AsEphemeral(true));
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User}#{Discriminator} - Member not in a voice channel", ctx.CommandName, ctx.Member.Username, ctx.Member.Discriminator);
                    return false;
                }
            }
            if (checkDeviceSelected)
            {
                // Returns false if the audio device the bot is currently using, or the corresponding capture instance, is null
                if (botInstance.Capture == null || botInstance.AudioDevice == null)
                {
                    ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "No audio device is selected")).AsEphemeral(true));
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User}#{Discriminator} - No active audio device", ctx.CommandName, ctx.Member.Username, ctx.Member.Discriminator);
                    return false;
                }
            }
            if (checkBotStreaming)
            {
                // Returns false if the bot is currently not capturing audio
                if (botInstance.Capture != null && botInstance.Capture.CaptureState != CaptureState.Capturing)
                {
                    ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "Bot is not streaming")).AsEphemeral(true));
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User}#{Discriminator} - Bot not capturing", ctx.CommandName, ctx.Member.Username, ctx.Member.Discriminator);
                    return false;
                }
            }

            if (checkBotNotStreaming)
            {
                // Returns false if the bot is currently capturing audio
                if (botInstance.Capture != null && botInstance.Capture.CaptureState != CaptureState.Stopped)
                {
                    ctx.CreateResponseAsync(InteractionResponseType.ChannelMessageWithSource, new DiscordInteractionResponseBuilder().AddEmbed
                        (Utils.GenerateEmbed(DiscordColor.Red, "Bot is already streaming")).AsEphemeral(true));
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User}#{Discriminator} - Bot already capturing", ctx.CommandName, ctx.Member.Username, ctx.Member.Discriminator);
                    return false;
                }
            }

            // If no checks have failed, return true
            return true;
        }

        /// <summary>
        /// Checks whether a DiscordMember has any of the roles defined as admin roles
        /// </summary>
        /// <param name="ctxMember">The DiscordMember whose roles to check</param>
        /// <param name="adminRoles">A list of role names to check against</param>
        /// <returns>True if the member has any of the roles, false if not</returns>
        private static bool CheckIfAdminRole(DiscordMember ctxMember, List<String> adminRoles)
        {
            foreach (DiscordRole role in ctxMember.Roles)
            {
                if (adminRoles.Contains(role.Name))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Generates an embed message
        /// </summary>
        /// <param name="color">The color of the embed as a DiscordColor object</param>
        /// <param name="title">The title of the embed</param>
        /// <param name="description">The description of the embed</param>
        /// <returns>The built DiscordEmbed object</returns>
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
        /// Generates an embed message
        /// </summary>
        /// <param name="color">The color of the embed as a DiscordColor object</param>
        /// <param name="description">The description of the embed</param>
        /// <returns>The built DiscordEmbed object</returns>
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
