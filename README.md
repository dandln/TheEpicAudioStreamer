# TheEpicAudioStreamer
This simple Discord bot application uses [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus) and [NAudio](https://github.com/naudio/NAudio) to stream audio directly from a local output device to a voice channel in real time.

Because the bot needs access to your audio devices, it cannot simply be invited to your server like most other bots. Instead, you will need to host the bot yourself on the machine that you want to stream audio from.

### System Requirements
* Windows 10
* [.NET Core 3.1 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet-core/current/runtime) (not required if self contained executable is used)
* A stable internet connection (see ["Known Issues"](#user-content-known-issues))

## Contents of this Readme
1. [Setting Up](#user-content-setting-up)
2. [Running the Bot](#user-content-running-the-bot)
3. [Discord Commands](#user-content-discord-commands)
4. [Known Issues](#user-content-known-issues)
5. [Command Line Arguments](#user-content-command-line-arguments)
6. [Changelog](#user-content-changelog)

## Setting Up
To run the bot, you will need to set up your own application and bot account on the [Discord Developer Portal](https://discord.com/developers/applications). If you already know how to do this, or already have a bot account that you want to use, feel free to skip ahead to ["Running the Bot"](#user-content-running-the-bot).
1. Go to the [Applications](https://discord.com/developers/applications) section of the Discord Developer Portal, log in to your Discord account if you are not logged in already and click the button "New Application" in the top right corner.
2. Enter a name for your application, which should be the same as the one you want your bot account to have, and click "Create".
3. You can enter a description and upload an image for your application, this will be displayed when you invite your bot to your server.
4. Select "Bot" from the settings sidebar, click "Add Bot" and acknowledge the warning with "Yes, do it!"
5. Head to the "OAuth2" section in the settings sidebar, tick the "bot" checkbox in the "Scopes" panel. Scroll down and make sure the following Bot Permissions are selected: `View Channels`, `Send Messages`, `Embed Links`, `Read Message History`, `Connect`, `Speak`.
6. Use the generated link to invite the bot to your server.
7. Go back to the "Bot" section, click on "Click to Reveal Token", and copy your bot token somewhere safe, you will need it to run the bot.

## Running the Bot
1. Download the latest version of the executable from the ["Releases" section of this repository](https://github.com/TheEpicSnowWolf/TheEpicAudioStreamer/releases/).
2. Place a text file called `bottoken.txt` that contains your bot token (gathered in step 7 of ["Setting Up"](#user-content-setting-up)) in the same directory as the executable. Alternatively, you can pass either a path to the token file or the token itself to the application via the `-t` command line argument.
3. Run the executable.
4. A list of your active audio output devices will be displayed, type the ID of the audio device that you want to use (the number in square brackets in front of the device name) into the command prompt and press enter. *__DO NOT use the same device that your Discord client uses for output.__ I recommend using a virtual audio device solution like [VB-Cable](https://vb-audio.com/Cable/), as this will ensure that you always hear the same thing as the others in your voice channel, and you have more control over which applications you stream.*
5. The application will now connect to your Discord bot and is ready to receive commands in the text channels of your server as long as the application window is open or until you terminate the process in the command prompt.

## Discord Commands
Type these commands in any text channel on your server. The bot listens to commands from any user who has the "Manage Server" permission and optionally a user specified through [command line arguments](#user-content-command-line-arguments).
* `!join` joins the current voice channel.
* `!start` starts streaming. Needs to be connected to a voice channel first.
* `!joinst` joins the current voice channel and immediately starts streaming.
* `!stop` stops streaming.
* `!refresh` restarts streaming. Useful if the streamed audio lags behind due to connection issues (see ["Known Issues"](#user-content-known-issues)).
* `!leave` stops streaming and disconnects from the current voice channel.
* `!help` shows command help in Discord.

You can change the command prefix from `!` to whatever you want via the `-p` command line argument. For example, you could change it to `.` to avoid conflicts with other bots on your server, making the commands `.join` etc.

## Known Issues
If you have an unstable internet connection (e.g. lag spikes), the captured audio that the bot was not able to send in time will 'pile up' instead of just being skipped, resulting in your streamed audio being behind what is actually happening on your machine. Even after some lengthy debugging, I was unable to find out whether this issue is caused by DSharpPlus, the Wasapi audio capturing or by my implementation of either, and I could not find a way to fix this yet.

As a workaround, I have implemented the `!refresh` command, which is essentially a shorthand to running `!stop` and `!start` in succession. This should reset the stream to the standard buffer delay of <1 sec.

## Command Line Arguments:
TheEpicAudioStreamer supports the following command line arguments:
* `-t` - Either a path to a text file that contains the bot token or the string of the bot token to use instead of the default token file.
* `-p` - A custom command prefix to use within Discord instead of `!`.
* `-d` - A friendly device name (the string in brackets in the device list) to use as a device. If a valid name is given, this skips the user prompt to select a device on application startup.
* `-a` - A Discord user that the bot should accept commands from, in addition to server managers. Format: `<Username>#<Discriminator>`
* `-v` - Enables debug messages from DSharpPlus.

## Changelog
### v0.4.1 - 2021-04-16
* Added simple version check on startup.
* Updated dependencies.

DSharpPlus version | NAudio version | Command Line Parser version
------------------ | -------------- | ---------------------------
4.0.0 | 2.0.0 | 2.9.0-preview1

### v0.4.0 - 2021-02-24
* Added `-a` command line option to specify a user that the bot should accept commands from, in addition to server managers.
* Updated dependencies.

DSharpPlus version | NAudio version | Command Line Parser version
------------------ | -------------- | ---------------------------
4.0.0-rc2 | 2.0.0 | 2.9.0-preview1

### v0.3.0 - 2021-02-06
* Added `-d` command line option to pass a friendly device name to the application for use to skip manual selection.
* Added `-v` command line option to display debug messages from DSharpPlus.
* Updated dependencies.

DSharpPlus version | NAudio version | Command Line Parser version
------------------ | -------------- | ---------------------------
4.0.0-rc1 | 2.0.0 | 2.9.0-preview1

### v0.2.1 - 2021-01-15
* Added option to pass the bot token directly via command line argument.
* Added option to change the command prefix via command line argument.
* Fixed potential memory leak.
* Updated dependencies.

DSharpPlus version | NAudio version | Command Line Parser version
------------------ | -------------- | ---------------------------
4.0.0-rc1 | 2.0.0-beta2 | 2.9.0-preview1

### v0.1.0 - 2021-01-02
* Initial release.

DSharpPlus version | NAudio version | Command Line Parser version
------------------ | -------------- | ---------------------------
4.0.0-rc1 | 2.0.0-beta1 | 2.9.0-preview1
