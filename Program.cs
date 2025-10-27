using System.Diagnostics;
using System.Text.Json;

class Bardnith
{
    static void Main(string[] args)
    {
        JsonDocument json = GetJson();
        Console.WriteLine(GetInfo(json));
    }

    static JsonDocument GetJson()
    {
        Console.Write("Enter video ID (string at the end of URL) ");
        string url = Console.ReadLine() ?? "https://www.youtube.com/watch?v=dQw4w9WgXcQ";

        ProcessStartInfo psi = new ProcessStartInfo
        {
            FileName = "yt-dlp_linux",
            Arguments = $"--dump-json --no-warnings {url}",
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        string output = "";

        try
        {
            using Process? process = Process.Start(psi);
            output = process!.StandardOutput.ReadToEnd();
            process?.WaitForExit();
        }
        catch (NullReferenceException ex)
        {
            Console.WriteLine("Caught null error: " + ex.Message);
        }

        JsonDocument json;

        if (output != null)
        {
            json = JsonDocument.Parse(output);
            return json;
        }

        return null!;
    }

    static string GetInfo(JsonDocument json)
    {
        string title;
        title = json.RootElement.GetProperty("title").GetString() ?? "No Title";
        int duration = json.RootElement.GetProperty("duration").GetInt32();
        int min = duration / 60;
        int sec = duration % 60;

        return $"{title} - ({min}:{sec})";
    }
}
