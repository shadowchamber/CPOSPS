using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace IIGCore.HardwareStation.PosTerminalWeb.Engine
{


public sealed class OposPipe : IOposBus
{
    private readonly SimulatorEngine _engine;
    private readonly string _folder;
    private readonly FileSystemWatcher _watcher;
    private readonly HashSet<string> _lights = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MockOPOSLight", "PTWLight" };
    private readonly Dictionary<string, string> _seen = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public OposPipe(SimulatorEngine engine, string folder)
    {
        _engine = engine;
        _folder = folder;
        Directory.CreateDirectory(folder);
        LoadLightNames();
        try
        {
            _watcher = new FileSystemWatcher(folder)
            {
                Filter = "*.*",
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                IncludeSubdirectories = false,
                EnableRaisingEvents = true
            };
            _watcher.Changed += (_, e) => Ingest(e.FullPath);
            _watcher.Created += (_, e) => Ingest(e.FullPath);
            _watcher.Renamed += (_, e) => Ingest(e.FullPath);
        }
        catch
        {
            _watcher = null;
        }
        foreach (var name in KnownFiles().Where(n => !_lights.Contains(Path.GetFileNameWithoutExtension(n))))
        {
            var path = Path.Combine(folder, name);
            if (File.Exists(path))
                Ingest(path);
        }
        foreach (var path in _lights
            .Select(name => Path.Combine(folder, name + ".txt"))
            .Where(File.Exists)
            .OrderBy(File.GetLastWriteTimeUtc))
            Ingest(path);
    }

    public void Write(string fileName, params string[] lines)
    {
        var path = Path.Combine(_folder, fileName);
        var text = string.Join(Environment.NewLine, lines) + Environment.NewLine;
        lock (_seen)
            _seen[fileName] = text;
        for (var i = 0; i < 5; i++)
        {
            try
            {
                File.WriteAllLines(path, lines);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(20);
            }
        }
    }

    public void Dispose() => _watcher?.Dispose();

    private IEnumerable<string> KnownFiles()
    {
        foreach (var light in _lights)
            yield return light + ".txt";
        yield return "MockOPOSDrawer1.txt";
        yield return "MockOPOSDrawer2.txt";
        yield return "MockOPOSLineDisplay.txt";
        yield return "MockOPOSPrinter.html";
        yield return "MockOPOSPinPadAMOUNT.txt";
        yield return "MockOPOSPinPadPIN.txt";
        yield return "MockOPOSScale.txt";
        yield return "MockOPOSScanner.txt";
        yield return "MockOPOSMSR.txt";
        yield return "MockOPOSSignatureCaptureOPOSSIGCAPSTATE.txt";
        yield return "MockOPOSSignatureCaptureOPOSSIGCAPDATA.txt";
        yield return "MockOPOSCoinDispenser.txt";
        yield return "MockOPOSRemoteOrderDisplay.txt";
        yield return "MOCKPAYMENTTERMINALPTOPEN.txt";
        yield return "MOCKPAYMENTTERMINALPTPAYMENTDETAILS.txt";
        yield return "MOCKPAYMENTTERMINALPTERROR.txt";
        yield return "MOCKPAYMENTTERMINALPTLINEITEMS.txt";
        yield return "MOCKPAYMENTTERMINALPTCARDSTATE.txt";
        yield return "MOCKPAYMENTTERMINALPTSIGCAPSTATE.txt";
        yield return "MOCKPAYMENTTERMINALPTSIGCAPDATA.txt";
        yield return "MOCKPAYMENTTERMINALPTCARDDATA.txt";
        yield return "MOCKPAYMENTTERMINALPTAUTHDATA.txt";
        yield return "MOCKPAYMENTTERMINALPTPAYMENTPROPERTIES.txt";
    }

    private void LoadLightNames()
    {
        // Virtual-only: never read ServiceOPOS. Built-in names cover POS MockOPOS + PTW profiles.
        _lights.Add("MockOPOSLight");
        _lights.Add("PTWLight");
    }

    private void Ingest(string path)
    {
        var file = Path.GetFileName(path);
        if (!KnownFiles().Contains(file, StringComparer.OrdinalIgnoreCase))
            return;
        var text = ReadText(path);
        if (text == null)
            return;
        lock (_seen)
        {
            if (_seen.TryGetValue(file, out var prior) && prior == text)
                return;
            _seen[file] = text;
        }
        var lines = SplitLines(text);
        _engine.Mutate(s => Apply(s, file, lines));
    }

    private void Apply(SimulatorState s, string file, string[] lines)
    {
        if (lines.Length == 0)
            return;
        var stem = Path.GetFileNameWithoutExtension(file);
        if (_lights.Contains(stem))
        {
            var parts = lines[0].Split('|');
            var action = (parts.Length > 0 ? parts[0] : "") ?? "";
            SimulatorEngine.ApplyLight(s, action, Int(parts, 1), Int(parts, 2), Int(parts, 3), Int(parts, 4, 262144), Int(parts, 5));
            SimulatorEngine.AddLog(s, "Light", action, lines[0]);
            return;
        }
        switch (file.ToUpperInvariant())
        {
            case "MOCKOPOSDRAWER1.TXT":
                s.MainDrawerOpen = lines[0].Equals("open", StringComparison.OrdinalIgnoreCase);
                s.Mpos.DrawerStatus = s.MainDrawerOpen ? "Open" : "Closed";
                break;
            case "MOCKOPOSDRAWER2.TXT":
                s.SecondaryDrawerOpen = lines[0].Equals("open", StringComparison.OrdinalIgnoreCase);
                break;
            case "MOCKOPOSLINEDISPLAY.TXT":
                s.LineDisplay = string.Join(Environment.NewLine, lines);
                s.Mpos.LineDisplayText = s.LineDisplay;
                break;
            case "MOCKOPOSPRINTER.HTML":
                s.ReceiptLines.Clear();
                s.ReceiptLines.Add(string.Join(Environment.NewLine, lines));
                break;
            case "MOCKOPOSPINPADAMOUNT.TXT":
                if (lines.Length > 1 && decimal.TryParse(lines[1], NumberStyles.Number, CultureInfo.CurrentCulture, out var amount))
                    s.PinAmount = amount;
                break;
            case "MOCKOPOSPINPADPIN.TXT":
                if (lines.Length > 1)
                    s.PinValue = lines[1];
                break;
            case "MOCKOPOSSCALE.TXT":
                if (decimal.TryParse(lines[0], NumberStyles.Number, CultureInfo.CurrentCulture, out var weight))
                    s.ScaleGrams = (int)Math.Round(weight * 1000m);
                break;
            case "MOCKOPOSSCANNER.TXT":
                s.LastBarcode = lines[0];
                s.Mpos.Barcode = lines[0];
                break;
            case "MOCKOPOSMSR.TXT":
                s.Mpos.SwipeTrack = lines.Length > 1 && lines[0] == "E" ? lines[1] : lines[0];
                break;
            case "MOCKOPOSSIGNATURECAPTUREOPOSSIGCAPDATA.TXT":
                if (lines.Length > 1)
                    s.Mpos.HexSignature = lines[1];
                break;
            case "MOCKOPOSCOINDISPENSER.TXT":
            {
                var amountLine = lines.Length > 1 && lines[0].Equals("Dispense", StringComparison.OrdinalIgnoreCase)
                    ? lines[1]
                    : lines[0];
                if (int.TryParse(amountLine, NumberStyles.Integer, CultureInfo.InvariantCulture, out var coins))
                    s.CoinChange = coins;
                s.CoinsArrived = true;
                break;
            }
            case "MOCKOPOSREMOTEORDERDISPLAY.TXT":
                s.OrderNotifications.Insert(0, new OrderNote { At = DateTime.Now, Text = string.Join(Environment.NewLine, lines) });
                break;
            default:
                if (file.StartsWith("MOCKPAYMENTTERMINAL", StringComparison.OrdinalIgnoreCase) && lines.Length > 1)
                {
                    s.PaymentState = lines[0];
                    s.PaymentLines = string.Join(Environment.NewLine, lines.Skip(1));
                }
                break;
        }
        SimulatorEngine.AddLog(s, "OPOS", file, lines[0]);
    }

    private static int Int(string[] parts, int index, int fallback = 0)
        => index < parts.Length && int.TryParse(parts[index], out var value) ? value : fallback;

    private static string[] SplitLines(string text)
    {
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        if (lines.Length > 0 && lines[lines.Length - 1].Length == 0)
            Array.Resize(ref lines, lines.Length - 1);
        return lines;
    }

    private static string ReadText(string path)
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            catch (IOException)
            {
                Thread.Sleep(20);
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
        return null;
    }
}
}
