using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Xml.Linq;

namespace IIGCore.HardwareStation.PosTerminalWeb.Engine
{


public sealed partial class SimulatorEngine
{
    private readonly object _gate = new object();
    private readonly string _storePath;
    private readonly string _documents;
    private readonly JsonSerializerSettings _json = new JsonSerializerSettings { Formatting = Formatting.Indented };

    public SimulatorEngine(string contentRoot, string documentsFolder, bool watchOpos, bool seedDefaults, IOposBus opos = null)
    {
        var dataDir = Path.Combine(contentRoot, "App_Data");
        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(documentsFolder);
        _storePath = Path.Combine(dataDir, "store.json");
        _documents = documentsFolder;
        State = Load(seedDefaults);
        Opos = opos ?? (watchOpos ? new OposPipe(this, _documents) : new MemoryOposBus());
    }

    public IOposBus Opos { get; }

    public SimulatorState State { get; private set; }

    public SimulatorState Snapshot()
    {
        lock (_gate)
        {
            return JsonConvert.DeserializeObject<SimulatorState>(JsonConvert.SerializeObject(State, _json), _json);
        }
    }

    public SimulatorState Mutate(Action<SimulatorState> action)
    {
        lock (_gate)
        {
            action(State);
            State.Revision++;
            Save();
            return Snapshot();
        }
    }

    public object Station(string route, JToken body)
    {
        lock (_gate)
        {
            var result = DispatchStation(route, body);
            Save();
            return result;
        }
    }

    private object DispatchStation(string route, JToken body)
    {
        var s = State;
        string device = Str(body, "deviceName") ?? Str(body, "DeviceName") ?? "";
        Log(s, "HardwareStation", route, device);
        switch (route)
        {
            case "Deviceinfo/GetOposDeviceDrivers":
                return StationCatalog.OposDrivers;
            case "BarcodeScanner/Lock":
                // Drop any barcode queued while unlocked — otherwise first poll after login
                // fires extended-logon / scan with a stale value the cashier never scanned now.
                s.LastBarcode = "";
                s.Mpos.Barcode = "";
                s.Mpos.BarcodeLocked = true;
                s.Mpos.BarcodeToken = Guid.NewGuid().ToString("N");
                return new { token = s.Mpos.BarcodeToken };
            case "BarcodeScanner/GetBarcodes":
            {
                // Consume-on-read: POS polls this in a loop; sticky barcodes re-fire forever.
                // Unlocked → empty (not 400) so POS error dialogs do not flood before Open/Lock.
                if (!s.Mpos.BarcodeLocked)
                {
                    s.LastBarcode = "";
                    s.Mpos.Barcode = "";
                    return "";
                }
                var barcode = s.LastBarcode ?? "";
                s.LastBarcode = "";
                return barcode;
            }
            case "BarcodeScanner/UnLock":
                s.Mpos.BarcodeLocked = false;
                s.Mpos.BarcodeToken = null;
                return new { ok = true };
            case "CashDrawer/Claim":
            {
                // OPOS Open() — claim device only. Does NOT raise the drawer sensor (IsOpen).
                var drawerDevice = (device ?? "").ToLowerInvariant();
                var secondary = drawerDevice.Contains("drawer2") || drawerDevice.Contains("secondary");
                if (!secondary && s.Mpos.DrawerStatus != "Open")
                    s.Mpos.DrawerStatus = "Claimed";
                Log(s, "CashDrawer", "Claim", secondary ? "secondary" : "main");
                return new { ok = true, drawer = secondary ? "secondary" : "main", claimed = true };
            }
            case "CashDrawer/Release":
            {
                // OPOS Close() — release claim only. Does NOT push the drawer shut.
                var drawerDevice = (device ?? "").ToLowerInvariant();
                var secondary = drawerDevice.Contains("drawer2") || drawerDevice.Contains("secondary");
                if (!secondary && !s.MainDrawerOpen)
                    s.Mpos.DrawerStatus = "Closed";
                Log(s, "CashDrawer", "Release", secondary ? "secondary" : "main");
                return new { ok = true, drawer = secondary ? "secondary" : "main", claimed = false };
            }
            case "CashDrawer/Open":
            {
                // Physical kick (OPOS OpenDrawer) — UI shows open; IsOpen becomes true.
                var drawerDevice = (device ?? "").ToLowerInvariant();
                var secondary = drawerDevice.Contains("drawer2") || drawerDevice.Contains("secondary");
                if (secondary)
                    s.SecondaryDrawerOpen = true;
                else
                {
                    s.MainDrawerOpen = true;
                    s.Mpos.DrawerStatus = "Open";
                }
                Opos.Write(secondary ? "MockOPOSDrawer2.txt" : "MockOPOSDrawer1.txt", "open");
                return new { ok = true, drawer = secondary ? "secondary" : "main" };
            }
            case "CashDrawer/Close":
            {
                // Physical close — cashier / SIM UI / test API pushing the drawer shut.
                var drawerDevice = (device ?? "").ToLowerInvariant();
                var secondary = drawerDevice.Contains("drawer2") || drawerDevice.Contains("secondary");
                if (secondary)
                    s.SecondaryDrawerOpen = false;
                else
                {
                    s.MainDrawerOpen = false;
                    s.Mpos.DrawerStatus = "Closed";
                }
                Opos.Write(secondary ? "MockOPOSDrawer2.txt" : "MockOPOSDrawer1.txt", "close");
                return new { ok = true, drawer = secondary ? "secondary" : "main" };
            }
            case "CashDrawer/IsOpen":
            {
                var drawerDevice = (device ?? "").ToLowerInvariant();
                if (drawerDevice.Contains("drawer2") || drawerDevice.Contains("secondary"))
                    return s.SecondaryDrawerOpen;
                return s.MainDrawerOpen;
            }
            case "LineDisplay/Lock":
                s.Mpos.LineDisplayLocked = true;
                s.Mpos.LineDisplayToken = Guid.NewGuid().ToString("N");
                return new { token = s.Mpos.LineDisplayToken };
            case "LineDisplay/UnLock":
                s.Mpos.LineDisplayLocked = false;
                s.Mpos.LineDisplayToken = null;
                return new { ok = true };
            case "LineDisplay/DisplayText":
            {
                // Auto-claim if POS DisplayText races ahead of Lock — never 400-flood the register.
                if (!s.Mpos.LineDisplayLocked)
                {
                    s.Mpos.LineDisplayLocked = true;
                    s.Mpos.LineDisplayToken = Guid.NewGuid().ToString("N");
                }
                var lines = ReadLines(body);
                s.LineDisplay = string.Join(Environment.NewLine, lines);
                s.Mpos.LineDisplayText = s.LineDisplay;
                Opos.Write("MockOPOSLineDisplay.txt", lines.ToArray());
                return new { ok = true };
            }
            case "LineDisplay/ClearDisplay":
                if (!s.Mpos.LineDisplayLocked)
                    return new { ok = true };
                s.LineDisplay = "";
                s.Mpos.LineDisplayText = "";
                Opos.Write("MockOPOSLineDisplay.txt", "");
                return new { ok = true };
            case "Msr/Lock":
                // Drop swipe queued while unlocked (SIM UI / prior session). HW_PTW opens MSR
                // on the sign-in screen; a leftover gift/loyalty track becomes
                // "extended logon credentials cannot be found" with no cashier swipe on logon.
                s.Mpos.SwipeAccount = "";
                s.Mpos.SwipeTrack = "";
                s.Mpos.SwipeTrack2 = "";
                s.Mpos.MsrLocked = true;
                s.Mpos.MsrToken = Guid.NewGuid().ToString("N");
                return new { token = s.Mpos.MsrToken };
            case "Msr/GetMsrSwipeInfo":
            {
                // Consume-on-read: POS polls MSR continuously; sticky track data causes
                // never-ending "extended logon credentials cannot be found" dialogs.
                // Unlocked → empty (not 400) so Open/Lock races do not flood POS dialogs.
                if (!s.Mpos.MsrLocked)
                {
                    s.Mpos.SwipeAccount = "";
                    s.Mpos.SwipeTrack = "";
                    s.Mpos.SwipeTrack2 = "";
                    return new { accountNumber = "", track1 = "", track2 = "" };
                }
                var swipe = new
                {
                    accountNumber = s.Mpos.SwipeAccount ?? "",
                    track1 = s.Mpos.SwipeTrack ?? "",
                    track2 = s.Mpos.SwipeTrack2 ?? ""
                };
                s.Mpos.SwipeAccount = "";
                s.Mpos.SwipeTrack = "";
                s.Mpos.SwipeTrack2 = "";
                return swipe;
            }
            case "Msr/UnLock":
                s.Mpos.MsrLocked = false;
                s.Mpos.MsrToken = null;
                s.Mpos.SwipeAccount = "";
                s.Mpos.SwipeTrack = "";
                s.Mpos.SwipeTrack2 = "";
                return new { ok = true };
            case "PinPad/Lock":
                s.Mpos.PinLocked = true;
                s.Mpos.PinToken = Guid.NewGuid().ToString("N");
                return s.Mpos.PinToken;
            case "PinPad/Unlock":
                s.Mpos.PinLocked = false;
                s.Mpos.PinToken = null;
                return new { ok = true };
            case "PinPad/GetPinEntry":
                if (!s.Mpos.PinLocked)
                    return new { encryptedPin = "", additionalSecurityInfo = "" };
                s.Mpos.WaitingPin = true;
                return new { encryptedPin = s.PinValue, additionalSecurityInfo = s.Mpos.AdditionalSecurityData };
            case "PinPad/CancelOperation":
                s.Mpos.WaitingPin = false;
                return new { ok = true };
            case "Printer/Print":
                var text = Str(body, "lines") ?? Str(body, "Lines") ?? Str(body, "text") ?? body.ToString();
                s.ReceiptLines.Add(text);
                Opos.Write("MockOPOSPrinter.html", text);
                return new { ok = true };
            case "Scale/Read":
                return s.ScaleGrams / 1000m;
            case "SignatureCapture/Lock":
                s.Mpos.SignatureLocked = true;
                s.Mpos.SignatureToken = Guid.NewGuid().ToString("N");
                return s.Mpos.SignatureToken;
            case "SignatureCapture/Unlock":
                s.Mpos.SignatureLocked = false;
                s.Mpos.SignatureToken = null;
                return new { ok = true };
            case "SignatureCapture/GetSignature":
                if (!s.Mpos.SignatureLocked)
                    return new { signature = "", signatureData = "" };
                s.Mpos.WaitingSignature = true;
                return new { signature = s.SignatureStrokes, signatureData = s.Mpos.HexSignature };
            case "SignatureCapture/CancelOperation":
                s.Mpos.WaitingSignature = false;
                return new { ok = true };
            case "RemoteOrderDisplay/DisplayMessage":
                var message = Str(body, "message") ?? Str(body, "Message") ?? body.ToString();
                s.OrderNotifications.Insert(0, new OrderNote { At = DateTime.Now, Text = message });
                s.Mpos.OrderMessage = message;
                Opos.Write("MockOPOSRemoteOrderDisplay.txt", "DisplayText", message);
                return new { ok = true };
            case "RemoteOrderDisplay/ClearDisplay":
                s.OrderNotifications.Clear();
                s.Mpos.OrderMessage = "";
                Opos.Write("MockOPOSRemoteOrderDisplay.txt", "ClearDisplay");
                return new { ok = true };
            case "Light/SwitchOn":
            {
                var lightNumber = Int(body, "number", 1);
                var blinkOn = Int(body, "blinkOn", 0);
                var blinkOff = Int(body, "blinkOff", 0);
                var color = Int(body, "color", 262144);
                var alarm = Int(body, "alarm", 0);
                ApplyLight(s, "SwitchOn", lightNumber, blinkOn, blinkOff, color, alarm);
                Opos.Write("MockOPOSLight.txt", $"SwitchOn|{lightNumber}|{blinkOn}|{blinkOff}|{color}|{alarm}");
                return new { ok = true, isOn = s.Light.IsOn, status = s.Light.Status, color = s.Light.Color };
            }
            case "Light/SwitchOff":
            {
                var lightNumber = Int(body, "number", 1);
                ApplyLight(s, "SwitchOff", lightNumber, 0, 0, 0, 0);
                Opos.Write("MockOPOSLight.txt", $"SwitchOff|{lightNumber}");
                return new { ok = true, isOn = s.Light.IsOn, status = s.Light.Status, color = s.Light.Color };
            }
            case "CoinDispenser/DispenseChange":
            {
                var amount = Int(body, "amount", 0);
                if (amount <= 0)
                    amount = Int(body, "change", s.CoinChange);
                s.CoinChange = amount;
                s.CoinsArrived = true;
                Opos.Write("MockOPOSCoinDispenser.txt", "Dispense", s.CoinChange.ToString());
                s.CoinsArrived = false;
                return new { ok = true, amount = s.CoinChange };
            }
            case "PaymentTerminal/Lock":
                s.Mpos.PaymentLocked = true;
                s.Mpos.PaymentToken = Guid.NewGuid().ToString("N");
                s.PaymentState = "PaymentOpenTerminal";
                return s.Mpos.PaymentToken;
            case "PaymentTerminal/Unlock":
                s.Mpos.PaymentLocked = false;
                s.Mpos.PaymentToken = null;
                return new { ok = true };
            case "PaymentTerminal/UpdateLineItems":
                Require(s.Mpos.PaymentLocked, "Payment terminal is not locked.");
                s.PaymentState = "LineItems";
                s.PaymentLines = Str(body, "lines") ?? body.ToString();
                Opos.Write("MOCKPAYMENTTERMINALPTLINEITEMS.txt", "PTLINEITEMS", s.PaymentLines);
                return new { ok = true };
            case "PaymentTerminal/AuthorizePayment":
                Require(s.Mpos.PaymentLocked, "Payment terminal is not locked.");
                return FinishPayment(s, "Authorize", approved: true);
            case "PaymentTerminal/CapturePayment":
                Require(s.Mpos.PaymentLocked, "Payment terminal is not locked.");
                return FinishPayment(s, "Capture", approved: true);
            case "PaymentTerminal/RefundPayment":
                Require(s.Mpos.PaymentLocked, "Payment terminal is not locked.");
                return FinishPayment(s, "Refund", approved: true);
            case "PaymentTerminal/VoidPayment":
                Require(s.Mpos.PaymentLocked, "Payment terminal is not locked.");
                return FinishPayment(s, "Void", approved: true);
            default:
                throw new InvalidOperationException($"Unsupported Hardware Station action '{route}'.");
        }
    }

    private object FinishPayment(SimulatorState s, string op, bool approved)
    {
        s.PaymentState = op;
        s.Mpos.LastPaymentOperation = op;
        s.Mpos.IsApproved = approved;
        s.Mpos.CardNumberMasked = Mask(s.Mpos.PaymentCardNumber);
        s.Mpos.PaymentErrors = approved ? "" : "Declined";
        Opos.Write("MOCKPAYMENTTERMINALPTAUTHDATA.txt", op, approved ? "Approved" : "Declined", s.Mpos.PaymentCardNumber ?? "");
        Opos.Write("MOCKPAYMENTTERMINALPTPAYMENTPROPERTIES.txt", op, s.Mpos.TotalAmount ?? "", s.Mpos.Currency ?? "");
        return new
        {
            isApproved = approved,
            cardNumberMasked = s.Mpos.CardNumberMasked,
            cardType = s.Mpos.PaymentCardType,
            signatureData = s.SignatureStrokes,
            paymentSdkData = op,
            errors = s.Mpos.PaymentErrors
        };
    }

    public SimulatorState Ui(string action, JToken body)
    {
        return Mutate(s => ApplyUi(action, body, s));
    }

    private SimulatorState Load(bool seedDefaults)
    {
        SimulatorState state;
        if (File.Exists(_storePath))
        {
            state = JsonConvert.DeserializeObject<SimulatorState>(File.ReadAllText(_storePath), _json) ?? new SimulatorState();
        }
        else
        {
            state = new SimulatorState();
            if (seedDefaults)
            {
                state.Barcodes.Add(new BarcodeItem { Number = "012345678905", Type = "Item", Label = "Code128" });
                state.Cards.Add(BuildCard("4111111111111111", "Visa", 12, DateTime.Now.Year + 2));
                state.Cards.Add(BuildCard("6011000990139424", "Loyalty", 6, DateTime.Now.Year + 2));
                state.Cards.Add(BuildCard("6011111111111117", "Loyalty", 9, DateTime.Now.Year + 1));
                state.Cards.Add(BuildCard("6006491234567890", "Gift", 1, DateTime.Now.Year + 4));
                state.Scales.Add(new ScaleItem { Name = "US scale", System = "English - US", Unit = "Pound - Lb" });
                state.Scales.Add(new ScaleItem { Name = "EU scale", System = "Europe - EU", Unit = "Kilogram - Kg" });
            }
        }
        ImportDesktopXml(state);
        return state;
    }

    private void ImportDesktopXml(SimulatorState state)
    {
        TryImport(Path.Combine(_documents, "BarcodeInfo.xml"), doc =>
        {
            foreach (var el in doc.Descendants("Barcode"))
            {
                var item = new BarcodeItem
                {
                    Number = el.Element("BarcodeNumber")?.Value ?? "",
                    Type = el.Element("BarcodeType")?.Value ?? "Item",
                    Label = el.Element("LabelType")?.Value ?? "Code128"
                };
                if (!string.IsNullOrWhiteSpace(item.Number) && state.Barcodes.All(b => b.Number != item.Number || b.Type != item.Type || b.Label != item.Label))
                    state.Barcodes.Add(item);
            }
        });
        TryImport(Path.Combine(_documents, "ScaleInfo.xml"), doc =>
        {
            foreach (var el in doc.Descendants("Scale"))
            {
                var item = new ScaleItem
                {
                    Name = el.Element("Name")?.Value ?? "",
                    System = el.Element("System")?.Value ?? "",
                    Unit = el.Element("Unit")?.Value ?? ""
                };
                if (!string.IsNullOrWhiteSpace(item.Name) && state.Scales.All(x => x.Name != item.Name))
                    state.Scales.Add(item);
            }
        });
    }

    private static void TryImport(string path, Action<XDocument> read)
    {
        try
        {
            if (File.Exists(path))
                read(XDocument.Load(path));
        }
        catch
        {
        }
    }

    private void Save()
    {
        File.WriteAllText(_storePath, JsonConvert.SerializeObject(State, _json));
    }

    public static CardItem BuildCard(string number, string type, int month, int year)
    {
        var mm = month.ToString("00");
        var track = $"%B{number}^FDCS{type}/{type}^{year}{mm}10110001111A123456789012?;{number}={year}{mm}1011000001234567?";
        return new CardItem
        {
            Number = number,
            Type = type,
            Month = type == "Gift" ? null : mm,
            Year = type == "Gift" ? (int?)null : year,
            Track = track
        };
    }

    public static string BuildTrack(CardItem card)
    {
        if (!string.IsNullOrWhiteSpace(card.Track) && !card.Track.StartsWith("%B", StringComparison.Ordinal))
            return card.Track;
        var year = card.Year?.ToString() ?? "";
        var month = card.Month ?? "";
        return $"%B{card.Number}^FDCS{card.Type}/{card.Type}^{year}{month}10110001111A123456789012?;{card.Number}={year}{month}1011000001234567?";
    }

    public static string WedgeEscape(string value) => value.Replace("%", "{%}").Replace("^", "{^}");

    private static string Str(JToken body, string name)
    {
        if (body == null || body.Type != JTokenType.Object)
            return null;
        var value = ((JObject)body).GetValue(name, StringComparison.OrdinalIgnoreCase);
        if (value == null || value.Type == JTokenType.Null)
            return null;
        if (value.Type == JTokenType.String)
            return value.Value<string>();
        if (value.Type == JTokenType.Boolean)
            return value.Value<bool>() ? "true" : "false";
        return value.ToString();
    }

    private static int Int(JToken body, string name, int fallback = 0)
    {
        var text = Str(body, name);
        return int.TryParse(text, out var value) ? value : fallback;
    }

    private static List<string> ReadLines(JToken body)
    {
        var list = new List<string>();
        if (body != null && body.Type == JTokenType.Object)
        {
            var lines = ((JObject)body).GetValue("lines", StringComparison.OrdinalIgnoreCase) as JArray;
            if (lines != null)
            {
                foreach (var item in lines)
                    list.Add(item.ToString());
                return list;
            }

            var text = Str(body, "displayText") ?? Str(body, "DisplayText");
            var qty = Str(body, "quantity") ?? Str(body, "Quantity");
            var price = Str(body, "price") ?? Str(body, "Price");
            if (text != null)
                list.Add(text);
            if (qty != null || price != null)
                list.Add($"{qty}    {price}".Trim());
        }

        if (list.Count == 0 && body != null && body.Type != JTokenType.None && body.Type != JTokenType.Null)
            list.Add(body.ToString(Formatting.None));
        return list;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void Log(SimulatorState s, string area, string action, string detail)
    {
        s.Logs.Insert(0, new LogItem { At = DateTime.Now, Area = area, Action = action, Detail = detail });
        if (s.Logs.Count > 200)
            s.Logs.RemoveRange(200, s.Logs.Count - 200);
    }

    public static void AddLog(SimulatorState s, string area, string action, string detail) => Log(s, area, action, detail);

    public static void ApplyLight(SimulatorState s, string action, int number, int blinkOn, int blinkOff, int color, int alarm)
    {
        if (action.Equals("SwitchOn", StringComparison.OrdinalIgnoreCase))
        {
            s.Light.IsOn = true;
            s.Light.Label = $"L{number}";
            s.Light.Color = color switch
            {
                65536 => "#d32f2f",
                131072 => "#f9a825",
                _ => "#2e7d32"
            };
            s.Light.BlinkOn = blinkOn;
            s.Light.BlinkOff = blinkOff;
            s.Light.Alarm = alarm;
            if (blinkOn > 0)
            {
                s.Light.Status = $"BLINKING  {blinkOn}ms / {blinkOff}ms";
                s.Light.Opacity = 1;
            }
            else
            {
                s.Light.Status = "ON";
                s.Light.Opacity = 1;
            }
            s.Light.Detail = $"Light {number}  ·  Color 0x{color:X}  ·  Alarm {alarm}";
        }
        else
        {
            s.Light.IsOn = false;
            s.Light.Label = $"L{number}";
            s.Light.Status = "OFF";
            s.Light.Color = "#d0d0d0";
            s.Light.Opacity = 0.25;
            s.Light.BlinkOn = 0;
            s.Light.Detail = $"Light {number}";
        }
    }

    private static string Mask(string number)
    {
        if (string.IsNullOrEmpty(number) || number.Length < 4)
            return number ?? "";
        return new string('*', number.Length - 4) + number.Substring(number.Length - 4);
    }
}

public sealed class SimulatorState
{
    public long Revision { get; set; }
    public List<BarcodeItem> Barcodes { get; set; } = new List<BarcodeItem>();
    public List<CardItem> Cards { get; set; } = new List<CardItem>();
    public List<ScaleItem> Scales { get; set; } = new List<ScaleItem>();
    public List<DeviceProfile> Devices { get; set; } = new List<DeviceProfile>();
    public int BarcodeIndex { get; set; }
    public int CardIndex { get; set; }
    public int ScaleIndex { get; set; }
    public int ScaleGrams { get; set; } = 1000;
    public bool MainDrawerOpen { get; set; }
    public bool SecondaryDrawerOpen { get; set; }
    public string LineDisplay { get; set; } = "Welcome to store";
    public string PinValue { get; set; } = "";
    public decimal PinAmount { get; set; }
    public List<string> ReceiptLines { get; set; } = new List<string>();
    public string SignatureStrokes { get; set; } = "";
    public string LastBarcode { get; set; } = "";
    public string LastWedge { get; set; } = "";
    public string PaymentState { get; set; } = "";
    public string PaymentLines { get; set; } = "Transaction is empty";
    public LightState Light { get; set; } = new LightState();
    public int CoinChange { get; set; }
    public bool CoinsArrived { get; set; }
    public List<OrderNote> OrderNotifications { get; set; } = new List<OrderNote>();
    public MposState Mpos { get; set; } = new MposState();
    public List<LogItem> Logs { get; set; } = new List<LogItem>();
    public int WedgeDelaySeconds { get; set; } = 2;
    public bool KeyboardWedge { get; set; }
}

public sealed class BarcodeItem
{
    public string Number { get; set; } = "";
    public string Type { get; set; } = "";
    public string Label { get; set; } = "";
}

public sealed class CardItem
{
    public string Number { get; set; } = "";
    public string Type { get; set; } = "";
    public string Month { get; set; }
    public int? Year { get; set; }
    public string Track { get; set; } = "";
}

public sealed class ScaleItem
{
    public string Name { get; set; } = "";
    public string System { get; set; } = "";
    public string Unit { get; set; } = "";
}

public sealed class DeviceProfile
{
    public string Peripheral { get; set; } = "";
    public string HardwareType { get; set; } = "OPOS";
    public string Name { get; set; } = "";
    public string Model { get; set; } = "";
    public string ConnectionType { get; set; } = "USB";
    public string IpAddress { get; set; } = "";
    public string HostName { get; set; } = "";
    public int Port { get; set; }
    public string FirmwareVersion { get; set; } = "";
    public string DriverVersion { get; set; } = "";
    public string DriverDownloadLink { get; set; } = "";
}

public sealed class LightState
{
    public bool IsOn { get; set; }
    public string Label { get; set; } = "L";
    public string Status { get; set; } = "OFF";
    public string Detail { get; set; } = "Waiting for signal…";
    public string Color { get; set; } = "#d0d0d0";
    public double Opacity { get; set; } = 0.25;
    public int BlinkOn { get; set; }
    public int BlinkOff { get; set; }
    public int Alarm { get; set; }
}

public sealed class OrderNote
{
    public DateTime At { get; set; }
    public string Text { get; set; } = "";
}

public sealed class LogItem
{
    public DateTime At { get; set; }
    public string Area { get; set; } = "";
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
}

public sealed class MposState
{
    public string CommunicationMethod { get; set; } = "Runtime";
    public string ManufacturerName { get; set; } = "";
    public string ManufacturerWebsite { get; set; } = "";
    public string SupportEmail { get; set; } = "";
    public string SupportTelephoneNumber { get; set; } = "";
    public string SupportWebsite { get; set; } = "";
    public string TechnicalContactName { get; set; } = "";
    public string TechnicalContactPhone { get; set; } = "";
    public string TechnicalContactEmail { get; set; } = "";
    public string DeviceName { get; set; } = "Mock";
    public string HardwareType { get; set; } = "OPOS";
    public bool LineDisplayLocked { get; set; }
    public string LineDisplayToken { get; set; }
    public string LineDisplayText { get; set; } = "";
    public string Quantity { get; set; } = "1";
    public string Price { get; set; } = "0.00";
    public bool UseBinaryConversion { get; set; }
    public string DrawerStatus { get; set; } = "None";
    public bool MsrLocked { get; set; }
    public string MsrToken { get; set; }
    public string SwipeAccount { get; set; } = "";
    public string SwipeTrack { get; set; } = "";
    public string SwipeTrack2 { get; set; } = "";
    public bool PinLocked { get; set; }
    public string PinToken { get; set; }
    public bool WaitingPin { get; set; }
    public string AccountNumber { get; set; } = "";
    public string AdditionalSecurityData { get; set; } = "";
    public bool PinExclusive { get; set; }
    public bool PinOverride { get; set; }
    public string TextToPrint { get; set; } = "";
    public string ReceiptLogoFilePath { get; set; } = "";
    public bool PrinterBinary { get; set; }
    public bool BarcodeLocked { get; set; }
    public string BarcodeToken { get; set; }
    public string Barcode { get; set; } = "";
    public bool SignatureLocked { get; set; }
    public string SignatureToken { get; set; }
    public bool WaitingSignature { get; set; }
    public string FormName { get; set; } = "";
    public bool SignatureExclusive { get; set; }
    public bool SignatureOverride { get; set; }
    public string HexSignature { get; set; } = "";
    public string OrderMessage { get; set; } = "";
    public bool PaymentLocked { get; set; }
    public string PaymentToken { get; set; }
    public bool IsExclusive { get; set; }
    public bool IsTestMode { get; set; } = true;
    public string InvoiceNumber { get; set; } = "INV-1";
    public string PaymentConnector { get; set; } = "TestConnector";
    public string Locale { get; set; } = "en-us";
    public string TerminalId { get; set; } = "TERM1";
    public string Currency { get; set; } = "USD";
    public string TotalAmount { get; set; } = "10.00";
    public string TaxAmount { get; set; } = "0.00";
    public string DiscountAmount { get; set; } = "0.00";
    public string SubTotalAmount { get; set; } = "10.00";
    public string Description { get; set; } = "Item";
    public string Sku { get; set; } = "SKU1";
    public string Upc { get; set; } = "012345678905";
    public string UnitPrice { get; set; } = "10.00";
    public string QuantityLine { get; set; } = "1";
    public bool IsVoided { get; set; }
    public string PaymentCardNumber { get; set; } = "";
    public string PaymentCardType { get; set; } = "";
    public string CardNumberMasked { get; set; } = "";
    public bool IsApproved { get; set; }
    public string PaymentErrors { get; set; } = "";
    public string LastPaymentOperation { get; set; } = "PayByCard";
    public string CurrentOperation { get; set; } = "PayByCard";
}
}
