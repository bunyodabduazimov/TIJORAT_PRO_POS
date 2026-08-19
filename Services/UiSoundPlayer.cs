using System.Media;
using System.IO;

namespace FFPOS.Services;

public static class UiSoundPlayer
{
    private static readonly Lazy<string> PinClickPath = new(GetPinClickPath);

    public static void PlayPinClick()
    {
        var path = PinClickPath.Value;
        if (!File.Exists(path))
        {
            return;
        }

        _ = Task.Run(() =>
        {
            try
            {
                using var player = new SoundPlayer(path);
                player.PlaySync();
            }
            catch
            {
            }
        });
    }

    private static string GetPinClickPath()
    {
        return Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "pip.wav");
    }
}
