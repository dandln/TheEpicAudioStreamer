using Microsoft.Extensions.Logging;
using DSharpPlus.Entities;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using DSharpPlus.Commands.Processors.SlashCommands;

namespace TEASLibrary
{
    /// <summary>
    /// Provides stataic helper functions for the audio streamer bot
    /// </summary>
    public static class Utils
    {
        /// <summary>
        /// Initialises the audio recorder instance bassed on the passed audio device.
        /// </summary>
        /// <returns>The initialised WasapiRecorder instance, or null if no device was initialised.</returns>
        /// <param name="audioDevice">The audio device to use, can be null if no device is used/available</param>
        public static WasapiRecorder? InitializeAudioRecorder(MMDevice? audioDevice)
        {
            if (audioDevice != null)
            {
                WasapiRecorder audioRecorder = new WasapiRecorderBuilder()
                    .WithDevice(audioDevice)
                    .WithBufferLength(100)
                    .WithFormat(new WaveFormat(48000, 16, 2))
                    .WithLoopbackCapture()
                    .Build();
                return audioRecorder;
            }
            else
            {
                return null;
            }
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
        public static async Task<bool> CheckCommandFeasibilityAsync(
            SlashCommandContext ctx,
            Bot botInstance,
            bool checkPermissions = false,
            bool checkBotConnected = false,
            bool checkBotNotConnected = false,
            bool checkUserConnected = false,
            bool checkDeviceSelected = false,
            bool checkBotStreaming = false,
            bool checkBotNotStreaming = false)
        {
            var connection = botInstance.CurrentConnection;

            if (checkPermissions)
            {
                // Return false if user is neither owner of the appliaction, server manager, flagged as an admin user nor has a role flagged as an admin role
                if (!ctx.Client.CurrentApplication.Owners!.Contains(ctx.User) &&
                    !ctx.Member!.PermissionsIn(ctx.Channel).HasFlag(DiscordPermission.ManageGuild) &&
                    !botInstance.BotConfig.AdminUsers.Contains(ctx.Member.Username) &&
                    !CheckIfAdminRole(ctx.Member, botInstance.BotConfig.AdminRoles))
                {
                    await ctx.RespondAsync(GenerateEmbed(DiscordColor.Red, $"Sorry {ctx.Member.Mention}, you're not the DJ today"), ephemeral: true);
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User} - Permission denied", ctx.Command.Name, ctx.Member.Username);
                    return false;
                }
            }
            if (checkBotConnected)
            {
                // Returns false if the bot is not currently connected to a voice channel
                if (connection == null)
                {
                    await ctx.RespondAsync(GenerateEmbed(DiscordColor.Red, "Bot is not connected to a voice channel"), ephemeral: true);
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User} - Bot not in a voice channel", ctx.Command.Name, ctx.Member!.Username);
                    return false;
                }
            }
            if (checkBotNotConnected)
            {
                // Returns false if the bot is already connected to a voice channel
                if (connection != null)
                {
                    await ctx.RespondAsync(GenerateEmbed(DiscordColor.Red, "Bot is already connected to a voice channel"), ephemeral: true);
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User} - Bot already in a voice channel", ctx.Command.Name, ctx.Member!.Username);
                    return false;
                }
            }
            if (checkUserConnected)
            {
                // Returns false if the member issuing the command is not currently connected to a voice channel
                if (ctx.Member!.VoiceState is null)
                {
                    await ctx.RespondAsync(GenerateEmbed(DiscordColor.Red, "You are not in a voice channel"), ephemeral: true);
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User} - Member not in a voice channel", ctx.Command.Name, ctx.Member.Username);
                    return false;
                }
            }
            if (checkDeviceSelected)
            {
                // Returns false if the audio device the bot is currently using is null
                if (botInstance.AudioDevice == null)
                {
                    await ctx.RespondAsync(GenerateEmbed(DiscordColor.Red, "No audio device is selected"), ephemeral: true);
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User} - No active audio device", ctx.Command.Name, ctx.Member!.Username);
                    return false;
                }
            }
            if (checkBotStreaming)
            {
                // Returns false if the bot is currently not capturing audio
                if (botInstance.Recorder != null && botInstance.Recorder.CaptureState != CaptureState.Capturing)
                {
                    await ctx.RespondAsync(GenerateEmbed(DiscordColor.Red, "Bot is not streaming"), ephemeral: true);
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User} - Bot not capturing", ctx.Command.Name, ctx.Member!.Username);
                    return false;
                }
            }

            if (checkBotNotStreaming)
            {
                // Returns false if the bot is currently capturing audio
                if (botInstance.Recorder != null && botInstance.Recorder.CaptureState != CaptureState.Stopped)
                {
                    await ctx.RespondAsync(GenerateEmbed(DiscordColor.Red, "Bot is already streaming"), ephemeral: true);
                    ctx.Client.Logger.LogWarning("Could not execute command {CommandName} issued by {User} - Bot already capturing", ctx.Command.Name, ctx.Member!.Username);
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
    }
}
