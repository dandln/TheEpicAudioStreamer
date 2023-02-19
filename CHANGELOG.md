# Changelog
### v0.6.1 - 2023-02-19
* HOTFIX: Removed bot token validation in config manager, as it failed to validate tokens created after a recent change in token format by Discord

DSharpPlus version | NAudio version | Command Line Parser version | Serilog version
------------------ | -------------- | --------------------------- | ---------------
 4.3.0 | 2.1.0 | 2.9.1 | 2.12.0

### v0.6.0 - 2023-02-18
* Implemented new configuration format, allowing more customisation and automatisation features
    * Bot now requires a Guild ID to connect to and register Slash Commands in
    * Added option to automatically connect to a given voice channel on startup
    * Added option to define multiple admin users and/or roles that can issue Slash Commands to the bot
    * Implemented interactive assistant to create configuration files
* Bot now always listenes to commands issued by the owner of the application
* Slash Commands are now registered on a Guild-basis instead of globally
* Internal code changes and cleanups
* Updated dependencies

DSharpPlus version | NAudio version | Command Line Parser version | Serilog version
------------------ | -------------- | --------------------------- | ---------------
 4.3.0 | 2.1.0 | 2.9.1 | 2.12.0

### v0.5.2 - 2022-04-03
* Implemented Serilog to facilitate more streamlined logging of both bot and app events.
* Updated dependencies (fixes an OAuth error in D#+).

DSharpPlus version | NAudio version | Command Line Parser version | Serilog version
------------------ | -------------- | --------------------------- | ---------------
 4.2.0-nightly-01105 | 2.0.1 | 2.9.0-preview1 | 2.10.0

### v0.5.0 - 2021-11-30
* Restructured project architecture:
    * Core bot functionality is now provided through a class library.
    * Console application acts as a wrapper for the library.
* Migrated the bot to Slash Commands.
* Removed `-p` command line option as it is no longer needed.
* Removed `refresh` command.
* Changed target framework to .NET 6.0.
* Updated dependencies.

DSharpPlus version | NAudio version | Command Line Parser version
------------------ | -------------- | ---------------------------
 4.2.0-nightly-01045 | 2.0.1 | 2.9.0-preview1

### v0.4.1 - 2021-04-16
* Added simple version check on startup.
* Updated dependencies.

DSharpPlus version | NAudio version | Command Line Parser version
------------------ | -------------- | ---------------------------
4.0.0-rc3 | 2.0.0 | 2.9.0-preview1

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
