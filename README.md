# TheEpicAudioStreamer (TEAS)
TEAS is a Discord bot that utilises [DSharpPlus](https://github.com/DSharpPlus/DSharpPlus) and [NAudio](https://github.com/naudio/NAudio) to stream audio directly from a local audio device to a voice channel.

The bot needs access to your local audio devices and can therefore not simply be invited to your server like most other bots. Instead, you will need to host the bot yourself on the machine that you want to stream audio from.

### System Requirements
* Windows 10 or above
* A stable internet connection

## Contents of this Readme
1. [Setting Up](#user-content-setting-up)
2. [Running the Bot](#user-content-running-the-bot)
3. [Discord Commands](#user-content-discord-commands)
4. [Command Line Arguments](#user-content-command-line-arguments)
5. [Known Issues](#user-content-known-issues)

## Setting Up
To run the bot, you will need to set up your own application and bot account on the [Discord Developer Portal](https://discord.com/developers/applications). If you already know how to do this, or already have a bot account that you want to use, feel free to skip ahead to ["Running the Bot"](#user-content-running-the-bot).
1. Go to the [Applications](https://discord.com/developers/applications) section of the Discord Developer Portal and click the button "New Application" in the top right corner.
2. Enter a name for your application, which should be the same as the one you want your bot account to have, and click "Create".
3. You can enter a description and upload an image for your application, this will be displayed when you invite your bot to your server.
4. Select "Bot" from the settings sidebar, click "Add Bot" and acknowledge the warning with "Yes, do it!"
5. Head back to the "General Information" tab and make a note of your "Application ID".
6. Replace `[APP_ID]` in the following URL with your application's ID and access it to invite your bot to your server.<br/>`https://discord.com/api/oauth2/authorize?client_id=[APP_ID]&permissions=2150648832&scope=bot%20applications.commands`<br />
This gives your bot the following permissions on your server: `Read Messages/View Channels`, `Send Messages`, `Embed Links`, `Use Slash Commands`, `Connect`, `Speak`
7. Go to the "Bot" section, click on "Click to Reveal Token", and copy your bot token somewhere safe, you will need it to run the bot.

## Running the Bot
1. Download the latest version of the executable from the ["Releases" section of this repository](https://github.com/TheEpicSnowWolf/TheEpicAudioStreamer/releases/).
2. Place a text file called `bottoken.txt` that contains your bot token (gathered in step 7 of ["Setting Up"](#user-content-setting-up)) in the same directory as the executable. Alternatively, you can pass either a path to the token file or the token itself to the application via the `-t` command line argument.
3. Run the executable.
4. A list of your active audio output devices will be displayed, type the ID of the audio device that you want to use (the number in square brackets in front of the device name) into the command prompt and press enter. I recommend using a virtual audio device solution like [VB-Cable](https://vb-audio.com/Cable/), as this will ensure that you always hear the same thing as the others in your voice channel, and you have more control over which applications you stream.*
5. The application will now connect to your Discord bot and is ready to receive commands in the text channels of your server as long as the application window is open or until you terminate the process in the command prompt.

## Discord Slash Commands
Type these slash commands in any text channel on your server. The bot listens to commands from any user who has the "Manage Server" permission and optionally a user specified through [command line arguments](#user-content-command-line-arguments).
* `/join` joins the current voice channel.
* `/start` starts streaming. Needs to be connected to a voice channel first.
* `/joinst` joins the current voice channel and immediately starts streaming.
* `/stop` stops streaming.
* `/leave` stops streaming and disconnects from the current voice channel.

## Command Line Arguments
TheEpicAudioStreamer supports the following command line arguments:
* `-t` - Either a path to a text file that contains the bot token or the string of the bot token to use instead of the default token file.
* `-d` - A friendly device name (the string in brackets in the device list) to use as a device. If a valid name is given, this skips the user prompt to select a device on application startup.
* `-a` - A Discord user that the bot should accept commands from, in addition to server managers. Format: `<Username>#<Discriminator>`
* `-v` - Enables debug messages from DSharpPlus.

## Known Issues
If the application is restarted shortly after being closed, Discord might throw the error "Invalid interaction application command" when issuing a Slash Command. Weirdly, it sometimes helps to run the `/leave` command. If this works (i.e. the command fails with "Bot is not connected to a voice channel."), other commands will work again as well.