# Bardnith

A C# Discord bot that streams YouTube audio directly to voice channels using FFmpeg and yt-dlp.

## Features

- Stream Youtube audio through Discord bot
- Command-based playback controls and queue management

## Requirements

- [.NET 8.0 SDK](https://dotnet.microsoft.com/)
- [yt-dlp](https://github.com/yt-dlp/yt-dlp)
- [FFmpeg](https://ffmpeg.org/download.html)
- [Discord Bot Token](https://discord.com/developers/applications)

## Installation

```bash
git clone https://github.com/Technith/Bardnith.git

cd Bardnith

dotnet restore

dotnet build

dotnet run
```

## Setup

1. Make sure ytdlp and ffmpeg are in your PATH or project directory
2. Create `.env` and store enviornment variables
```.env
API_KEY=<Discord Bot Token>
Guild_ID=<Server ID of your discord server>
```

## Usage

`/join` - Adds bot to user's current voice channel
`/leave` - Disconnects bot from user's current voice channel
`/add <Youtube URL>` - Adds youtube link to bot's queue
`/queue` - Lists current queue
`/skip` - Skips current audio
