using System;
using System.IO;

namespace IIGCore.HardwareStation.PosTerminalWeb
{
    /// <summary>
    /// In-process PosTerminal station engine settings.
    /// </summary>
    public sealed class PosTerminalWebOptions
    {
        public string ContentRoot { get; set; }

        public string OposFolder { get; set; }

        public bool WatchOpos { get; set; } = true;

        public static PosTerminalWebOptions FromEnvironment()
        {
            var options = new PosTerminalWebOptions();

            var contentRoot = Environment.GetEnvironmentVariable("PosTerminalWeb__ContentRoot")
                ?? Environment.GetEnvironmentVariable("POSTERMINALWEB_CONTENTROOT");
            if (string.IsNullOrWhiteSpace(contentRoot))
            {
                contentRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    "IIGCore",
                    "PosTerminalWeb");
            }

            options.ContentRoot = contentRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            var oposFolder = Environment.GetEnvironmentVariable("PosTerminalWeb__OposFolder")
                ?? Environment.GetEnvironmentVariable("POSTERMINALWEB_OPOSFOLDER");
            options.OposFolder = string.IsNullOrWhiteSpace(oposFolder)
                ? Path.Combine(options.ContentRoot, "App_Data", "opos")
                : oposFolder.Trim();

            var watch = Environment.GetEnvironmentVariable("PosTerminalWeb__WatchOpos");
            if (!string.IsNullOrWhiteSpace(watch) && bool.TryParse(watch, out var watchOpos))
            {
                options.WatchOpos = watchOpos;
            }

            return options;
        }
    }
}
