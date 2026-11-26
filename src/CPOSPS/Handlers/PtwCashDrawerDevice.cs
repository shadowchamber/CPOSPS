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
    /// Cash drawer → PosTerminal.Web with real OPOS semantics (all routes hit SIM):
    /// Open → Claim (no IsOpen), OpenDrawer → Open (sensor/UI open),
    /// Close → Release (no physical close), IsOpen → sensor.
    /// Physical close is CashDrawer/Close from SIM UI or test API (cashier push).
    /// </summary>
    public sealed class PtwCashDrawerDevice : INamedRequestHandlerAsync
    {
        private string _deviceName = PosTerminalWebConstants.Drawer1;

        public string HandlerName => PosTerminalWebConstants.HandlerName;

        public IEnumerable<Type> SupportedRequestTypes => new[]
        {
            typeof(OpenCashDrawerDeviceRequest),
            typeof(OpenDrawerCashDrawerDeviceRequest),
            typeof(CloseCashDrawerDeviceRequest),
            typeof(IsOpenCashDrawerDeviceRequest),
        };

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));
            var client = PosTerminalWebStationClient.Shared;

            switch (request)
            {
                case OpenCashDrawerDeviceRequest open:
                    _deviceName = PtwDeviceName.From(open, _deviceName);
                    await client.PostAsync("CashDrawer/Claim", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return NullResponse.Instance;

                case OpenDrawerCashDrawerDeviceRequest openDrawer:
                    _deviceName = PtwDeviceName.From(openDrawer, _deviceName);
                    await client.PostAsync("CashDrawer/Open", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return NullResponse.Instance;

                case CloseCashDrawerDeviceRequest close:
                    _deviceName = PtwDeviceName.From(close, _deviceName);
                    await client.PostAsync("CashDrawer/Release", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return NullResponse.Instance;

                case IsOpenCashDrawerDeviceRequest isOpen:
                    _deviceName = PtwDeviceName.From(isOpen, _deviceName);
                    var openState = await client.PostAsync<bool>("CashDrawer/IsOpen", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return new IsOpenCashDrawerDeviceResponse(openState);

                default:
                    throw new NotSupportedException($"Request '{request.GetType().FullName}' is not supported by {HandlerName} cash drawer.");
            }
        }
    }
}
