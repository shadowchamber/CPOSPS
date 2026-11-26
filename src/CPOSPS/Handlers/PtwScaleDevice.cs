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
    /// Scale → PosTerminal.Web Scale/Read.
    /// </summary>
    public sealed class PtwScaleDevice : INamedRequestHandlerAsync
    {
        private string _deviceName = PosTerminalWebConstants.Scale;

        public string HandlerName => PosTerminalWebConstants.HandlerName;

        public IEnumerable<Type> SupportedRequestTypes => new[]
        {
            typeof(OpenScaleDeviceRequest),
            typeof(ReadScaleDeviceRequest),
            typeof(CloseScaleDeviceRequest),
        };

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));
            var client = PosTerminalWebStationClient.Shared;

            switch (request)
            {
                case OpenScaleDeviceRequest open:
                    _deviceName = PtwDeviceName.From(open, _deviceName);
                    return NullResponse.Instance;

                case ReadScaleDeviceRequest read:
                    // SIM returns weight in kg as decimal; HWS response expects grams as uint.
                    var weightKg = await client.PostAsync<decimal>("Scale/Read", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    var grams = weightKg <= 0 ? 0u : (uint)decimal.Round(weightKg * 1000m, 0);
                    return new ReadScaleDeviceResponse(grams);

                case CloseScaleDeviceRequest close:
                    _deviceName = PtwDeviceName.From(close, _deviceName);
                    return NullResponse.Instance;

                default:
                    throw new NotSupportedException($"Request '{request.GetType().FullName}' is not supported by {HandlerName} scale.");
            }
        }
    }
}
