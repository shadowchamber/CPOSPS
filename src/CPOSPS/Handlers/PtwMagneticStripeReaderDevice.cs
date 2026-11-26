using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.HardwareStation.PeripheralRequests;
using Microsoft.Dynamics.Commerce.HardwareStation.Peripherals.Entities;
using Microsoft.Dynamics.Commerce.Runtime.Handlers;
using Microsoft.Dynamics.Commerce.Runtime.Messages;
using Newtonsoft.Json.Linq;

namespace IIGCore.HardwareStation.PosTerminalWeb.Handlers
{
    /// <summary>
    /// MSR → PosTerminal.Web Msr/* routes.
    /// </summary>
    public sealed class PtwMagneticStripeReaderDevice : INamedRequestHandlerAsync
    {
        private string _deviceName = PosTerminalWebConstants.Msr;

        public string HandlerName => PosTerminalWebConstants.HandlerName;

        public IEnumerable<Type> SupportedRequestTypes => new[]
        {
            typeof(OpenMagneticSwipeReaderDeviceRequest),
            typeof(SwipeMagneticSwipeReaderDeviceRequest),
            typeof(CloseMagneticSwipeReaderDeviceRequest),
        };

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));
            var client = PosTerminalWebStationClient.Shared;

            switch (request)
            {
                case OpenMagneticSwipeReaderDeviceRequest open:
                    _deviceName = PtwDeviceName.From(open, _deviceName);
                    await client.PostAsync("Msr/Lock", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return NullResponse.Instance;

                case SwipeMagneticSwipeReaderDeviceRequest swipe:
                    var json = await client.PostAsync<JToken>("Msr/GetMsrSwipeInfo", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    json = UnwrapObject(json);
                    var track1 = TokenString(json, "track1", "Track1", "track1Data", "Track1Data");
                    var track2 = TokenString(json, "track2", "Track2", "track2Data", "Track2Data");
                    // null MagneticCardSwipeInfo = no swipe (empty info makes POS treat as bad card / ATTENTION flood).
                    if (string.IsNullOrWhiteSpace(track1) && string.IsNullOrWhiteSpace(track2))
                    {
                        return new SwipeMagneticSwipeReaderDeviceResponse(null);
                    }

                    var info = new MagneticCardSwipeInfo();
                    info.ParseTracks(track1 ?? string.Empty, track2 ?? string.Empty);
                    var account = TokenString(json, "accountNumber", "AccountNumber");
                    if (!string.IsNullOrWhiteSpace(account))
                    {
                        info.AccountNumber = account;
                    }

                    return new SwipeMagneticSwipeReaderDeviceResponse(info);

                case CloseMagneticSwipeReaderDeviceRequest close:
                    _deviceName = PtwDeviceName.From(close, _deviceName);
                    await client.PostAsync("Msr/UnLock", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return NullResponse.Instance;

                default:
                    throw new NotSupportedException($"Request '{request.GetType().FullName}' is not supported by {HandlerName} MSR.");
            }
        }

        private static JToken UnwrapObject(JToken json)
        {
            if (json is JArray array && array.Count > 0)
            {
                return array[0];
            }

            return json;
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
