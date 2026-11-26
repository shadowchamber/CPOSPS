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
    /// Line display → PosTerminal.Web LineDisplay/* routes.
    /// </summary>
    public sealed class PtwLineDisplayDevice : INamedRequestHandlerAsync
    {
        private string _deviceName = PosTerminalWebConstants.LineDisplay;

        public string HandlerName => PosTerminalWebConstants.HandlerName;

        public IEnumerable<Type> SupportedRequestTypes => new[]
        {
            typeof(OpenLineDisplayDeviceRequest),
            typeof(DisplayTextLineDisplayDeviceRequest),
            typeof(ClearDisplayLineDisplayDeviceRequest),
            typeof(CloseLineDisplayDeviceRequest),
        };

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));
            var client = PosTerminalWebStationClient.Shared;

            switch (request)
            {
                case OpenLineDisplayDeviceRequest open:
                    _deviceName = PtwDeviceName.From(open, _deviceName);
                    await client.PostAsync("LineDisplay/Lock", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return new OpenLineDisplayDeviceResponse(40);

                case DisplayTextLineDisplayDeviceRequest display:
                    await client.PostAsync(
                        "LineDisplay/DisplayText",
                        new { deviceName = _deviceName, lines = display.Lines }).ConfigureAwait(false);
                    return NullResponse.Instance;

                case ClearDisplayLineDisplayDeviceRequest clear:
                    await client.PostAsync("LineDisplay/ClearDisplay", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return NullResponse.Instance;

                case CloseLineDisplayDeviceRequest close:
                    _deviceName = PtwDeviceName.From(close, _deviceName);
                    await client.PostAsync("LineDisplay/UnLock", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return NullResponse.Instance;

                default:
                    throw new NotSupportedException($"Request '{request.GetType().FullName}' is not supported by {HandlerName} line display.");
            }
        }
    }
}
