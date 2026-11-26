using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.HardwareStation.PeripheralRequests;
using Microsoft.Dynamics.Commerce.Runtime.Handlers;
using Microsoft.Dynamics.Commerce.Runtime.Messages;

namespace IIGCore.HardwareStation.PosTerminalWeb.Handlers
{
    /// <summary>
    /// Barcode scanner → PosTerminal.Web BarcodeScanner/* routes.
    /// </summary>
    public sealed class PtwBarcodeScannerDevice : INamedRequestHandlerAsync
    {
        private string _deviceName = PosTerminalWebConstants.Scanner;

        public string HandlerName => PosTerminalWebConstants.HandlerName;

        public IEnumerable<Type> SupportedRequestTypes => new[]
        {
            typeof(OpenBarcodeScannerDeviceRequest),
            typeof(GetBarcodesBarcodeScannerDeviceRequest),
            typeof(CloseBarcodeScannerDeviceRequest),
        };

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));
            var client = PosTerminalWebStationClient.Shared;

            switch (request)
            {
                case OpenBarcodeScannerDeviceRequest open:
                    _deviceName = PtwDeviceName.From(open, _deviceName);
                    await client.PostAsync("BarcodeScanner/Lock", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return NullResponse.Instance;

                case GetBarcodesBarcodeScannerDeviceRequest get:
                    _deviceName = PtwDeviceName.From(get, _deviceName);
                    var barcode = await client.PostAsync<string>("BarcodeScanner/GetBarcodes", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    var list = string.IsNullOrEmpty(barcode)
                        ? Array.Empty<string>()
                        : new[] { barcode };
                    return new GetBarcodesBarcodeScannerDeviceResponse(list);

                case CloseBarcodeScannerDeviceRequest close:
                    _deviceName = PtwDeviceName.From(close, _deviceName);
                    await client.PostAsync("BarcodeScanner/UnLock", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return NullResponse.Instance;

                default:
                    throw new NotSupportedException($"Request '{request.GetType().FullName}' is not supported by {HandlerName} scanner.");
            }
        }
    }
}
