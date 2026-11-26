using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace IIGCore.HardwareStation.PosTerminalWeb.Engine
{


public sealed partial class SimulatorEngine
{
    private void ApplyUi(string action, JToken body, SimulatorState s)
    {
        switch (action)
        {
            case "drawer":
            {
                var which = (T(body, "drawer") ?? "").Trim().Trim('"').ToLowerInvariant();
                var isSecondary = which is "secondary" or "sec" or "2" or "drawer2";
                if (isSecondary)
                    s.SecondaryDrawerOpen = !s.SecondaryDrawerOpen;
                else
                    s.MainDrawerOpen = !s.MainDrawerOpen;
                var open = isSecondary ? s.SecondaryDrawerOpen : s.MainDrawerOpen;
                if (!isSecondary)
                    s.Mpos.DrawerStatus = open ? "Open" : "Closed";
                AddLog(s, "CashDrawer", isSecondary ? "Secondary" : "Main", open ? "open" : "close");
                Opos.Write(isSecondary ? "MockOPOSDrawer2.txt" : "MockOPOSDrawer1.txt", open ? "open" : "close");
                break;
            }
            case "scan":
            {
                var barcode = s.Barcodes.ElementAtOrDefault(s.BarcodeIndex) ?? throw new InvalidOperationException("Select a barcode.");
                s.LastWedge = s.KeyboardWedge ? WedgeEscape(barcode.Number) : barcode.Number;
                Opos.Write("MockOPOSScanner.txt", barcode.Number);
                // Only queue for POS while scanner is locked. Unlocked queue survives until
                // next login Lock and fires as a surprise scan / extended logon.
                if (!s.Mpos.BarcodeLocked)
                {
                    AddLog(s, "Barcode", "ScanIgnored", "scanner not locked by POS");
                    break;
                }
                s.LastBarcode = barcode.Number;
                s.Mpos.Barcode = barcode.Number;
                AddLog(s, "Barcode", "Scan", s.LastWedge);
                break;
            }
            case "selectBarcode":
                s.BarcodeIndex = int.Parse(T(body, "index"));
                break;
            case "saveBarcode":
            {
                var item = new BarcodeItem { Number = T(body, "number").Trim(), Type = T(body, "type"), Label = T(body, "label") };
                if (string.IsNullOrWhiteSpace(item.Number) || string.IsNullOrWhiteSpace(item.Type) || string.IsNullOrWhiteSpace(item.Label))
                    throw new InvalidOperationException("Barcode number, type, and label are required.");
                var edit = T(body, "editIndex");
                var duplicate = s.Barcodes.Select((b, i) => (b, i)).Any(x => x.b.Number == item.Number && x.b.Type == item.Type && x.b.Label == item.Label && x.i.ToString() != edit);
                if (duplicate)
                    throw new InvalidOperationException("That barcode already exists.");
                if (int.TryParse(edit, out var bi) && bi >= 0 && bi < s.Barcodes.Count)
                    s.Barcodes[bi] = item;
                else
                    s.Barcodes.Add(item);
                s.BarcodeIndex = s.Barcodes.FindIndex(b => b.Number == item.Number && b.Type == item.Type && b.Label == item.Label);
                break;
            }
            case "selectCard":
                s.CardIndex = int.Parse(T(body, "index"));
                break;
            case "saveCard":
            {
                var useNumber = T(body, "mode") != "track";
                var card = useNumber
                    ? BuildCard(T(body, "number").Trim(), T(body, "type"), int.Parse(string.IsNullOrWhiteSpace(T(body, "month")) ? "1" : T(body, "month")), int.Parse(string.IsNullOrWhiteSpace(T(body, "year")) ? DateTime.Now.Year.ToString() : T(body, "year")))
                    : new CardItem { Track = T(body, "track"), Type = T(body, "type"), Number = ParsePan(T(body, "track")) };
                if (card.Type == "Gift")
                {
                    card.Month = null;
                    card.Year = null;
                }
                if (string.IsNullOrWhiteSpace(card.Number) && string.IsNullOrWhiteSpace(card.Track))
                    throw new InvalidOperationException("Card number or track data is required.");
                if (string.IsNullOrWhiteSpace(card.Track))
                    card.Track = BuildTrack(card);
                var cardEdit = T(body, "editIndex");
                if (int.TryParse(cardEdit, out var ci) && ci >= 0 && ci < s.Cards.Count)
                    s.Cards[ci] = card;
                else
                    s.Cards.Add(card);
                s.CardIndex = Math.Max(0, s.Cards.Count - 1);
                break;
            }
            case "swipe":
            {
                var cardItem = s.Cards.ElementAtOrDefault(s.CardIndex) ?? throw new InvalidOperationException("Select a card.");
                var track = string.IsNullOrWhiteSpace(cardItem.Track) ? BuildTrack(cardItem) : cardItem.Track;
                s.LastWedge = s.KeyboardWedge ? WedgeEscape(track) : track;
                s.Mpos.PaymentCardNumber = cardItem.Number;
                s.Mpos.PaymentCardType = cardItem.Type;
                Opos.Write("MockOPOSMSR.txt", "E", track);
                var payTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Visa", "Master", "Amex", "Discover" };
                if (payTypes.Contains(cardItem.Type) || T(body, "channel") == "payment")
                {
                    s.PaymentState = "PTCARDDATA";
                    s.PaymentLines = track;
                    Opos.Write("MOCKPAYMENTTERMINALPTCARDSTATE.txt", "PTCARDSTATE", "1");
                    Opos.Write("MOCKPAYMENTTERMINALPTCARDDATA.txt", "PTCARDDATA", track);
                }
                // Only queue track for POS MSR poll while locked. Unlocked queue is delivered
                // on the next sign-in Lock → "extended logon credentials cannot be found"
                // even though the cashier never swiped on the login screen.
                if (!s.Mpos.MsrLocked)
                {
                    AddLog(s, "MSR", "SwipeIgnored", "MSR not locked by POS");
                    break;
                }
                s.Mpos.SwipeAccount = cardItem.Number;
                s.Mpos.SwipeTrack = track;
                AddLog(s, "MSR", "Swipe", cardItem.Number);
                break;
            }
            case "pin":
            {
                var key = T(body, "key");
                if (key is "clear" or "C")
                    s.PinValue = "";
                else if ((key is "back" or "⌫") && s.PinValue.Length > 0)
                    s.PinValue = s.PinValue.Substring(0, s.PinValue.Length - 1);
                else if (key is "enter" or "Enter" or "OK")
                {
                    s.Mpos.WaitingPin = false;
                    AddLog(s, "PinPad", "Enter", s.PinValue);
                    Opos.Write("MockOPOSPinPadPIN.txt", "PIN", s.PinValue);
                    Opos.Write("MockOPOSPinPadAMOUNT.txt", "AMOUNT", s.PinAmount.ToString(System.Globalization.CultureInfo.CurrentCulture));
                }
                else if (key.Length == 1 && char.IsDigit(key[0]) && s.PinValue.Length < 12)
                    s.PinValue += key;
                break;
            }
            case "pinAmount":
                s.PinAmount = decimal.TryParse(T(body, "amount"), out var amount) ? amount : 0;
                AddLog(s, "PinPad", "Amount", s.PinAmount.ToString());
                Opos.Write("MockOPOSPinPadAMOUNT.txt", "AMOUNT", s.PinAmount.ToString(System.Globalization.CultureInfo.CurrentCulture));
                break;
            case "print":
            {
                var receipt = T(body, "text");
                if (string.IsNullOrWhiteSpace(receipt))
                    receipt = s.Mpos.TextToPrint;
                if (string.IsNullOrWhiteSpace(receipt))
                    receipt = "STORE RECEIPT\nTOTAL 0.00";
                s.ReceiptLines.Add(receipt);
                AddLog(s, "Printer", "Print", receipt);
                Opos.Write("MockOPOSPrinter.html", receipt);
                break;
            }
            case "clearReceipt":
                s.ReceiptLines.Clear();
                Opos.Write("MockOPOSPrinter.html", "");
                break;
            case "removeBarcode":
                if (s.BarcodeIndex >= 0 && s.BarcodeIndex < s.Barcodes.Count)
                    s.Barcodes.RemoveAt(s.BarcodeIndex);
                s.BarcodeIndex = Math.Max(0, Math.Min(s.BarcodeIndex, s.Barcodes.Count - 1));
                break;
            case "removeCard":
                if (s.CardIndex >= 0 && s.CardIndex < s.Cards.Count)
                    s.Cards.RemoveAt(s.CardIndex);
                s.CardIndex = Math.Max(0, Math.Min(s.CardIndex, s.Cards.Count - 1));
                break;
            case "scaleWeight":
                s.ScaleGrams = int.Parse(T(body, "grams"));
                Opos.Write("MockOPOSScale.txt", (s.ScaleGrams / 1000m).ToString(System.Globalization.CultureInfo.CurrentCulture));
                break;
            case "selectScale":
                s.ScaleIndex = int.Parse(T(body, "index"));
                break;
            case "saveScale":
            {
                var scale = new ScaleItem { Name = T(body, "name").Trim(), System = T(body, "system"), Unit = T(body, "unit") };
                if (string.IsNullOrWhiteSpace(scale.Name))
                    throw new InvalidOperationException("Scale name is required.");
                var se = T(body, "editIndex");
                if (int.TryParse(se, out var si) && si >= 0 && si < s.Scales.Count)
                    s.Scales[si] = scale;
                else
                    s.Scales.Add(scale);
                s.ScaleIndex = s.Scales.FindIndex(x => x.Name == scale.Name);
                break;
            }
            case "removeScale":
                if (s.ScaleIndex >= 0 && s.ScaleIndex < s.Scales.Count)
                    s.Scales.RemoveAt(s.ScaleIndex);
                s.ScaleIndex = Math.Max(0, Math.Min(s.ScaleIndex, s.Scales.Count - 1));
                break;
            case "moveScale":
            {
                var dir = int.Parse(T(body, "dir"));
                var next = s.ScaleIndex + dir;
                if (next >= 0 && next < s.Scales.Count)
                    s.ScaleIndex = next;
                break;
            }
            case "signature":
                s.SignatureStrokes = T(body, "strokes");
                s.Mpos.HexSignature = ToHex(Encoding.UTF8.GetBytes(s.SignatureStrokes));
                s.Mpos.WaitingSignature = false;
                AddLog(s, "Signature", "Accept", s.Mpos.HexSignature);
                Opos.Write("MockOPOSSignatureCaptureOPOSSIGCAPDATA.txt", "OPOSSIGCAPDATA", s.Mpos.HexSignature);
                break;
            case "clearSignature":
                s.SignatureStrokes = "";
                s.Mpos.HexSignature = "";
                break;
            case "lineDisplay":
                s.LineDisplay = T(body, "text");
                s.Mpos.LineDisplayText = s.LineDisplay;
                Opos.Write("MockOPOSLineDisplay.txt", s.LineDisplay.Split('\n'));
                break;
            case "clearLineDisplay":
                s.LineDisplay = "";
                s.Mpos.LineDisplayText = "";
                Opos.Write("MockOPOSLineDisplay.txt", "");
                break;
            case "light":
            {
                var lightAction = T(body, "action");
                var lightNumber = int.Parse(string.IsNullOrWhiteSpace(T(body, "number")) ? "1" : T(body, "number"));
                var blinkOn = int.Parse(string.IsNullOrWhiteSpace(T(body, "blinkOn")) ? "0" : T(body, "blinkOn"));
                var blinkOff = int.Parse(string.IsNullOrWhiteSpace(T(body, "blinkOff")) ? "0" : T(body, "blinkOff"));
                var color = int.Parse(string.IsNullOrWhiteSpace(T(body, "color")) ? "262144" : T(body, "color"));
                var alarm = int.Parse(string.IsNullOrWhiteSpace(T(body, "alarm")) ? "0" : T(body, "alarm"));
                ApplyLight(s, lightAction, lightNumber, blinkOn, blinkOff, color, alarm);
                Opos.Write("MockOPOSLight.txt", lightAction.Equals("SwitchOff", StringComparison.OrdinalIgnoreCase)
                    ? $"SwitchOff|{lightNumber}"
                    : $"SwitchOn|{lightNumber}|{blinkOn}|{blinkOff}|{color}|{alarm}");
                break;
            }
            case "coinTest":
                s.CoinChange = new Random().Next(1, 200);
                s.CoinsArrived = true;
                AddLog(s, "CoinDispenser", "Test", s.CoinChange.ToString());
                Opos.Write("MockOPOSCoinDispenser.txt", "Dispense", s.CoinChange.ToString());
                s.CoinsArrived = false;
                break;
            case "coinDispense":
                s.CoinChange = int.Parse(string.IsNullOrWhiteSpace(T(body, "amount")) ? "0" : T(body, "amount"));
                s.CoinsArrived = true;
                AddLog(s, "CoinDispenser", "Dispense", s.CoinChange.ToString());
                Opos.Write("MockOPOSCoinDispenser.txt", "Dispense", s.CoinChange.ToString());
                s.CoinsArrived = false;
                break;
            case "order":
            {
                var note = T(body, "message");
                s.OrderNotifications.Insert(0, new OrderNote { At = DateTime.Now, Text = note });
                s.Mpos.OrderMessage = note;
                Opos.Write("MockOPOSRemoteOrderDisplay.txt", "DisplayText", note);
                break;
            }
            case "clearOrder":
                s.OrderNotifications.Clear();
                s.Mpos.OrderMessage = "";
                Opos.Write("MockOPOSRemoteOrderDisplay.txt", "ClearDisplay");
                break;
            case "wedge":
                s.KeyboardWedge = T(body, "enabled") == "true";
                if (int.TryParse(T(body, "delay"), out var delay))
                    s.WedgeDelaySeconds = delay;
                break;
            case "saveDevice":
            {
                var profile = new DeviceProfile
                {
                    Peripheral = T(body, "peripheral"),
                    HardwareType = T(body, "hardwareType"),
                    Name = T(body, "name"),
                    Model = T(body, "model"),
                    ConnectionType = T(body, "connectionType"),
                    IpAddress = T(body, "ipAddress"),
                    HostName = T(body, "hostName"),
                    Port = int.TryParse(T(body, "port"), out var port) ? port : 0,
                    FirmwareVersion = T(body, "firmwareVersion"),
                    DriverVersion = T(body, "driverVersion"),
                    DriverDownloadLink = T(body, "driverDownloadLink")
                };
                if (string.IsNullOrWhiteSpace(profile.Name))
                    throw new InvalidOperationException("Device name cannot be empty.");
                var existing = s.Devices.FindIndex(d => d.Peripheral == profile.Peripheral && d.Name == profile.Name);
                if (existing >= 0)
                    s.Devices[existing] = profile;
                else
                    s.Devices.Add(profile);
                s.Mpos.DeviceName = profile.Name;
                s.Mpos.HardwareType = profile.HardwareType;
                break;
            }
            case "saveSettings":
                s.Mpos.CommunicationMethod = T(body, "communicationMethod");
                s.Mpos.ManufacturerName = T(body, "manufacturerName");
                s.Mpos.ManufacturerWebsite = T(body, "manufacturerWebsite");
                s.Mpos.SupportEmail = T(body, "supportEmail");
                s.Mpos.SupportTelephoneNumber = T(body, "supportTelephoneNumber");
                s.Mpos.SupportWebsite = T(body, "supportWebsite");
                s.Mpos.TechnicalContactName = T(body, "technicalContactName");
                s.Mpos.TechnicalContactPhone = T(body, "technicalContactPhone");
                s.Mpos.TechnicalContactEmail = T(body, "technicalContactEmail");
                break;
            case "mposField":
                ApplyMposField(s, T(body, "field"), T(body, "value"));
                break;
            default:
                throw new InvalidOperationException($"Unknown UI action '{action}'.");
        }
    }

    private static void ApplyMposField(SimulatorState s, string field, string value)
    {
        var m = s.Mpos;
        switch (field)
        {
            case "quantity": m.Quantity = value; break;
            case "price": m.Price = value; break;
            case "binary": m.UseBinaryConversion = value == "true"; s.Mpos.PrinterBinary = value == "true"; break;
            case "textToPrint": m.TextToPrint = value; break;
            case "logo": m.ReceiptLogoFilePath = value; break;
            case "account": m.AccountNumber = value; break;
            case "security": m.AdditionalSecurityData = value; break;
            case "pinExclusive": m.PinExclusive = value == "true"; break;
            case "pinOverride": m.PinOverride = value == "true"; break;
            case "formName": m.FormName = value; break;
            case "sigExclusive": m.SignatureExclusive = value == "true"; break;
            case "sigOverride": m.SignatureOverride = value == "true"; break;
            case "orderMessage": m.OrderMessage = value; break;
            case "invoice": m.InvoiceNumber = value; break;
            case "connector": m.PaymentConnector = value; break;
            case "locale": m.Locale = value; break;
            case "terminal": m.TerminalId = value; break;
            case "currency": m.Currency = value; break;
            case "total": m.TotalAmount = value; break;
            case "tax": m.TaxAmount = value; break;
            case "discount": m.DiscountAmount = value; break;
            case "subtotal": m.SubTotalAmount = value; break;
            case "description": m.Description = value; break;
            case "sku": m.Sku = value; break;
            case "upc": m.Upc = value; break;
            case "unitPrice": m.UnitPrice = value; break;
            case "qtyLine": m.QuantityLine = value; break;
            case "voided": m.IsVoided = value == "true"; break;
            case "exclusive": m.IsExclusive = value == "true"; break;
            case "testMode": m.IsTestMode = value == "true"; break;
            case "operation": m.CurrentOperation = value; break;
            case "deviceName": m.DeviceName = value; break;
            case "hardwareType": m.HardwareType = value; break;
            default: throw new InvalidOperationException($"Unknown field '{field}'.");
        }
    }

    private static string T(JToken body, string name)
    {
        if (body == null || body.Type != JTokenType.Object)
            return "";
        var value = ((JObject)body).GetValue(name, StringComparison.OrdinalIgnoreCase);
        if (value == null || value.Type == JTokenType.Null)
            return "";
        if (value.Type == JTokenType.String)
            return value.Value<string>() ?? "";
        if (value.Type == JTokenType.Boolean)
            return value.Value<bool>() ? "true" : "false";
        return value.ToString();
    }

    private static string ParsePan(string track)
    {
        var start = track.IndexOf("%B", StringComparison.Ordinal);
        if (start < 0)
            return "";
        var end = track.IndexOf('^', start);
        return end > start ? track.Substring(start + 2, end - (start + 2)) : "";
    }

    private static string ToHex(byte[] bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        for (var i = 0; i < bytes.Length; i++)
        {
            sb.Append(bytes[i].ToString("X2"));
        }

        return sb.ToString();
    }
}
}
