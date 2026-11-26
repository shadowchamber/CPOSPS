using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.HardwareStation.PeripheralRequests;
using Microsoft.Dynamics.Commerce.Runtime.Handlers;
using Microsoft.Dynamics.Commerce.Runtime.Messages;

namespace IIGCore.HardwareStation.PosTerminalWeb.Handlers
{
    /// <summary>
    /// Printer → PosTerminal.Web Printer/Print.
    /// </summary>
    public sealed class PtwPrinterDevice : INamedRequestHandlerAsync
    {
        private string _deviceName = PosTerminalWebConstants.Printer;

        public string HandlerName => PosTerminalWebConstants.HandlerName;

        public IEnumerable<Type> SupportedRequestTypes => new[]
        {
            typeof(OpenPrinterDeviceRequest),
            typeof(PrintPrinterDeviceRequest),
            typeof(ClosePrinterDeviceRequest),
        };

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));
            var client = PosTerminalWebStationClient.Shared;

            switch (request)
            {
                case OpenPrinterDeviceRequest open:
                    _deviceName = PtwDeviceName.From(open, _deviceName);
                    return NullResponse.Instance;

                case PrintPrinterDeviceRequest print:
                    _deviceName = PtwDeviceName.From(print, _deviceName);
                    var text = BuildPrintText(print);
                    await client.PostAsync(
                        "Printer/Print",
                        new { deviceName = _deviceName, text, lines = text }).ConfigureAwait(false);
                    return NullResponse.Instance;

                case ClosePrinterDeviceRequest close:
                    _deviceName = PtwDeviceName.From(close, _deviceName);
                    return NullResponse.Instance;

                default:
                    throw new NotSupportedException($"Request '{request.GetType().FullName}' is not supported by {HandlerName} printer.");
            }
        }

        private static string BuildPrintText(PrintPrinterDeviceRequest print)
        {
            var sb = new StringBuilder();
            Append(sb, print.Header);
            Append(sb, print.Lines);
            Append(sb, print.Footer);
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return;
            }

            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.Append(value);
        }
    }
}
