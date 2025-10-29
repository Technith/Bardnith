using System.Diagnostics;
using System.Text.Json;
using System.Reflection;
using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Discord.Interactions;
using Discord.Audio;
using DotNetEnv;

class Bardnith
{
    private DiscordSocketClient? _client;
    private InteractionService? _interaction;

    public static async Task Main(string[] args)
    {
        var program = new Bardnith();
        await program.StartBotAsync();
    }

    public async Task StartBotAsync()
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.GuildVoiceStates | GatewayIntents.Guilds
        });

        _interaction = new InteractionService(_client.Rest);

        _client.Ready += OnReadyAsync;

        _client.Log += LogAsync;
        _interaction.Log += LogAsync;

        _client.InteractionCreated += HandleInteractionAsync;

        Env.Load();
        string token = Environment.GetEnvironmentVariable("API_KEY") ??
            throw new InvalidOperationException("Missing API_KEY environment variable.");

        try
        {
            await _client.LoginAsync(Discord.TokenType.Bot, token);
            await _client.StartAsync();

            await _interaction.AddModulesAsync(Assembly.GetEntryAssembly(), null);

            // Run Program Logic

            await Task.Delay(-1);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex}");
        }
    }

    async Task OnReadyAsync()
    {
        Console.WriteLine($"Logged in as {_client?.CurrentUser} on {_client?.Guilds.Count} servers.");

        var guild = _client!.GetGuild(622299639298654219);
        await _interaction!.RegisterCommandsToGuildAsync(guild.Id);
        Console.WriteLine("Slash commands registered.");
    }

    Task LogAsync(LogMessage msg)
    {
        Console.WriteLine($"[{msg.Severity}] {msg.Source}: {msg.Message}");
        if (msg.Exception != null) Console.WriteLine(msg.Exception);

        return Task.CompletedTask;
    }

    async Task HandleInteractionAsync(SocketInteraction interaction)
    {
        try
        {
            var ctx = new SocketInteractionContext(_client, interaction);
            await _interaction!.ExecuteCommandAsync(ctx, null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Error] {ex}");
        }
    }
}

public static class MusicQueue
{
    public static readonly ConcurrentQueue<string> songs = new ConcurrentQueue<string>();
}

public static class AudioClients
{
    private static readonly ConcurrentDictionary<ulong, IAudioClient> _clients = new();

    public static IAudioClient? Get(ulong guildId) =>
        _clients.TryGetValue(guildId, out var client) ? client : null;

    public static void Set(ulong guildId, IAudioClient client) =>
        _clients[guildId] = client;

    public static void Remove(ulong guildId) =>
        _clients.TryRemove(guildId, out _);
}

public class Playback
{
    public Task? PlaybackTask { get; set; }
    public Process? FfmpegProcess { get; set; }
    public Process? YtdlpProcess { get; set; }
}

// Change to get/set
// Add command to toggle autoplay on/off
public class PlaybackOptions
{
    public bool autoplay = true;
}

public static class PlaybackDictionary
{
    public static readonly Dictionary<ulong, Playback> Playbacks = new();
}

public class SlashModule : InteractionModuleBase<SocketInteractionContext>
{

    [SlashCommand("hello", "Greets the user.")]
    public async Task HelloAsync()
    {
        await RespondAsync($"Hello, {Context.User.Username}!");
    }

    [SlashCommand("fetchinfo", "Fetches video information")]
    public async Task LinkAsync([Summary("url", "Resource URL")] string url)
    {
        await DeferAsync();

        if (url.Length == 0)
        {
            await FollowupAsync("Invalid URL");
        }

        try
        {
            await FollowupAsync(await GetTitleDurationAsync(url));
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("add", "Add Song to Queue")]
    public async Task AddAsync([Summary("url", "Song URL")] string url)
    {
        await DeferAsync();

        if (url.Length == 0)
        {
            await FollowupAsync("Invalid URL");
        }

        try
        {
            MusicQueue.songs.Enqueue(url);
            await FollowupAsync($"Added {url} to queue");
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("queue", "List Current Queue")]
    public async Task QueueAsync()
    {
        await DeferAsync();

        string output = "**Current Queue:**\n\t";

        try
        {
            foreach (string song in MusicQueue.songs)
            {
                output += await GetTitleDurationAsync(song);
                output += "\n\t";
            }
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Error: {ex.Message}", ephemeral: true);
        }

        await FollowupAsync(output);
    }

    [SlashCommand("join", "Bot joins your voice channel safely")]
    public async Task JoinAsync()
    {
        var user = Context.User as SocketGuildUser;
        var channel = user?.VoiceChannel;

        if (channel == null)
        {
            await RespondAsync("Must be in voice channel");
            return;
        }

        var guild = Context.Guild as SocketGuild;
        if (guild == null)
        {
            await RespondAsync("Error: Could not get the guild instance", ephemeral: true);
            return;
        }

        if (!guild.CurrentUser.GuildPermissions.Connect)
        {
            await RespondAsync("Error: No permission to connect to channel", ephemeral: true);
            return;
        }

        await DeferAsync();

        var guildId = Context.Guild.Id;

        try
        {
            var audioClient = await channel.ConnectAsync(selfDeaf: true);
            AudioClients.Set(Context.Guild.Id, audioClient);
            await FollowupAsync($"Joined **{channel.Name}**");
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Failed to join voice channel: {ex.Message}", ephemeral: true);
        }
    }

    [SlashCommand("leave", "Disconnects the bot from the voice channel")]
    public async Task LeaveAsync()
    {
        var guildId = Context.Guild.Id;
        var audioClient = AudioClients.Get(guildId);
        if (audioClient == null)
        {
            await RespondAsync("Bot is not in voice channel");
            return;
        }

        await DeferAsync();

        try
        {
            await audioClient.StopAsync();   // Stops any audio
            audioClient.Dispose();           // Disconnects the bot
            AudioClients.Remove(guildId);    // Remove from the dictionary
            await Task.Delay(1000);          // Give Discord a moment to release the session

            await FollowupAsync("Disconnected from the voice channel.");
        }
        catch (Exception ex)
        {
            await FollowupAsync($"Error disconnecting: {ex.Message}");
        }
    }

    [SlashCommand("play", "Plays from queue in current voice channel")]
    async Task PlayAsync()
    {
        if (!MusicQueue.songs.TryDequeue(out string? url))
        {
            await RespondAsync("Queue is empty");
            return;
        }

        var guildId = Context.Guild.Id;
        var audioClient = AudioClients.Get(guildId);
        if (audioClient == null)
        {
            await RespondAsync("Bot is not in voice channel");
            return;
        }

        var options = new PlaybackOptions();

        var playback = new Playback();
        PlaybackDictionary.Playbacks[guildId] = playback;

        playback.PlaybackTask = Task.Run(async () =>
           {
               try
               {
                   // Get the audio URL
                   var ytdlpProcess = new Process
                   {
                       StartInfo = new ProcessStartInfo
                       {
                           FileName = "yt-dlp_linux",
                           Arguments = $"-f bestaudio -g {url}",
                           RedirectStandardOutput = true,
                           UseShellExecute = false,
                           CreateNoWindow = true
                       }
                   };
                   ytdlpProcess.Start();
                   playback.YtdlpProcess = ytdlpProcess;
                   string? audioUrl = await ytdlpProcess.StandardOutput.ReadLineAsync();
                   ytdlpProcess.WaitForExit();

                   // Start FFmpeg
                   var ffmpegProcess = new Process
                   {
                       StartInfo = new ProcessStartInfo
                       {
                           FileName = "ffmpeg",
                           Arguments = $"-i \"{audioUrl}\" -ac 2 -f s16le -ar 48000 pipe:1",
                           RedirectStandardOutput = true,
                           UseShellExecute = false,
                           CreateNoWindow = true
                       }
                   };
                   ffmpegProcess.Start();
                   playback.FfmpegProcess = ffmpegProcess;

                   using var output = ffmpegProcess.StandardOutput.BaseStream;
                   using var discordStream = audioClient.CreatePCMStream(AudioApplication.Music);

                   try
                   {
                       await output.CopyToAsync(discordStream);
                       await discordStream.FlushAsync();
                   }
                   finally
                   {
                       playback.FfmpegProcess.Kill();
                       playback.YtdlpProcess.Kill();
                       playback.PlaybackTask = null;
                   }
               }
               catch (Exception ex)
               {
                   Console.WriteLine($"Error while playing audio: {ex}");
                   await RespondAsync($"Error while playing audio: {ex.Message}");
                   return;
               }

               if (options.autoplay)
               {
                   await Task.Delay(1000);
                   await PlayAsync();
               }
           });

        // Respond immediately so bot is free for other commands
        await RespondAsync($"Now playing: {url}");
    }

    [SlashCommand("skip", "Skips current song")]
    async Task SkipAsync()
    {
        var guildId = Context.Guild.Id;
        if (!PlaybackDictionary.Playbacks.TryGetValue(guildId, out var playback))
        {
            await RespondAsync("Nothing currently plyaing.");
            return;
        }

        try
        {
            playback.FfmpegProcess?.Kill();
            playback.YtdlpProcess?.Kill();
            await RespondAsync("Skipped current song");
        }
        catch (Exception ex)
        {
            await RespondAsync($"Failed to skip song: {ex.Message}");
        }
    }

    async Task<JsonDocument> GetJsonAsync(string url)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "yt-dlp_linux",
            Arguments = $"--dump-json --no-warnings {url}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(psi) ??
            throw new InvalidOperationException("Invalid Process");

        string output = await process.StandardOutput.ReadToEndAsync();

        await process.WaitForExitAsync();

        JsonDocument json;

        if (output != null)
        {
            json = JsonDocument.Parse(output);
            return json;
        }

        return null!;
    }

    static string ParseJson(JsonDocument json)
    {
        string title;
        title = json.RootElement.GetProperty("title").GetString() ?? "No Title";
        int duration = json.RootElement.GetProperty("duration").GetInt32();
        int min = duration / 60;
        int sec = duration % 60;

        return $"{title} - ({min}:{sec})";
    }

    async Task<string> GetTitleDurationAsync(string url)
    {
        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "yt-dlp_linux",
            Arguments = $"--print \"%(title)s (%(duration_string)s)\" {url}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using Process process = Process.Start(psi) ??
            throw new InvalidOperationException("Invalid Process");

        string output = await process.StandardOutput.ReadToEndAsync();

        await process.WaitForExitAsync();

        return output;
    }
}
