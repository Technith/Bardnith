using System.Diagnostics;
using System.Text.Json;

class Bardnith
{
    static void Main()
    {
        Console.WriteLine("Title: " + GetTitle());
    }

    static string GetTitle()
    {
        Console.WriteLine("Enter video ID (string at the end of URL)");
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
        string title = "No Title";

        if (output != null)
        {
            json = JsonDocument.Parse(output);
            title = json.RootElement.GetProperty("title").GetString() ?? "No Title";
        }

        return title;
    }
}
