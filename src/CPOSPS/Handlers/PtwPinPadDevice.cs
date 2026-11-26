using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.HardwareStation.PeripheralRequests;
using Microsoft.Dynamics.Commerce.HardwareStation.Peripherals;
using Microsoft.Dynamics.Commerce.Runtime.Handlers;
using Microsoft.Dynamics.Commerce.Runtime.Messages;
using Newtonsoft.Json.Linq;

namespace IIGCore.HardwareStation.PosTerminalWeb.Handlers
{
    /// <summary>
    /// PIN pad → PosTerminal.Web PinPad/* routes.
    /// </summary>
    public sealed class PtwPinPadDevice : INamedRequestHandlerAsync
    {
        private string _deviceName = PosTerminalWebConstants.PinPad;
        private string _token;

        public string HandlerName => PosTerminalWebConstants.HandlerName;

        public IEnumerable<Type> SupportedRequestTypes => new[]
        {
            typeof(LockPinPadDeviceRequest),
            typeof(GetPinEntryPinPadDeviceRequest),
            typeof(AbortTransactionPinPadDeviceRequest),
            typeof(ClosePinPadDeviceRequest),
        };

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));
            var client = PosTerminalWebStationClient.Shared;

            switch (request)
            {
                case LockPinPadDeviceRequest lockReq:
                    _deviceName = PtwDeviceName.From(lockReq, _deviceName);
                    _token = await client.PostForTokenAsync("PinPad/Lock", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return new LockPinPadDeviceResponse(_token);

                case GetPinEntryPinPadDeviceRequest get:
                    var json = await client.PostAsync<JToken>(
                        "PinPad/GetPinEntry",
                        new
                        {
                            deviceName = _deviceName,
                            token = _token,
                            amount = get.Amount,
                            accountNumber = get.AccountNumber,
                        }).ConfigureAwait(false);
                    var results = new PinPadResults(
                        aborted: false,
                        status: 0,
                        encryptedPin: TokenString(json, "encryptedPin", "EncryptedPin") ?? string.Empty,
                        additionalSecurityInfo: TokenString(json, "additionalSecurityInfo", "AdditionalSecurityInfo") ?? string.Empty);
                    return new GetPinEntryPinPadDeviceResponse(results);

                case AbortTransactionPinPadDeviceRequest abort:
                    await client.PostAsync("PinPad/CancelOperation", new { deviceName = _deviceName, token = _token }).ConfigureAwait(false);
                    return NullResponse.Instance;

                case ClosePinPadDeviceRequest close:
                    await client.PostAsync("PinPad/Unlock", new { deviceName = _deviceName, token = _token }).ConfigureAwait(false);
                    _token = null;
                    return NullResponse.Instance;

                default:
                    throw new NotSupportedException($"Request '{request.GetType().FullName}' is not supported by {HandlerName} PIN pad.");
            }
        }

        private static string TokenString(JToken json, params string[] names)
        {
            if (json == null)
            {
                return null;
            }

            foreach (var name in names)
            {
                var t = json[name];
                if (t != null && t.Type != JTokenType.Null)
                {
                    return t.ToString();
                }
            }

            return null;
        }
    }
}
