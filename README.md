# catbotcode

A basic Mastodon bot that will post random images from a given folder, written in C# using .NET Core, with no external libraries used.

This is the source code used to power the bot at <a rel="nofollow me" href="https://botsin.space/@emmacatbot">@emmacatbot<span>@botsin.space</span></a>

## Usage

**Requirement:** [.NET Core SDK](https://dotnet.microsoft.com/download), version 3.1 or later.

1. Compile the project. This can be done using the .NET Core SDK, by typing `dotnet build` into a command line.
    * Windows users can use Visual Studio 2019 to compile the solution.
2. Place a `config.ini` file (example provided at `config-example.ini`, replace the values with your own) into the output folder (likely `bin/Debug/netcoreapp3.1` in the project folder)
3. Run the bot by typing `dotnet catbotcode.dll` into a command line.
    * A different configuration file can be specified on the command line; `dotnet catbotcode.dll path/to/config.ini`
