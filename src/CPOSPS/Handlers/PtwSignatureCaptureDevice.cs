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
    /// Signature capture → PosTerminal.Web SignatureCapture/* routes.
    /// </summary>
    public sealed class PtwSignatureCaptureDevice : INamedRequestHandlerAsync
    {
        private string _deviceName = PosTerminalWebConstants.SignatureCapture;
        private string _token;

        public string HandlerName => PosTerminalWebConstants.HandlerName;

        public IEnumerable<Type> SupportedRequestTypes => new[]
        {
            typeof(LockSignatureCaptureDeviceRequest),
            typeof(GetSignatureSignatureCaptureDeviceRequest),
            typeof(StopCaptureSignatureCaptureDeviceRequest),
            typeof(CloseSignatureCaptureDeviceRequest),
        };

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));
            var client = PosTerminalWebStationClient.Shared;

            switch (request)
            {
                case LockSignatureCaptureDeviceRequest lockReq:
                    _deviceName = PtwDeviceName.From(lockReq, _deviceName);
                    _token = await client.PostForTokenAsync("SignatureCapture/Lock", PtwDeviceName.BodyWithDevice(_deviceName)).ConfigureAwait(false);
                    return new LockSignatureCaptureDeviceResponse(_token);

                case GetSignatureSignatureCaptureDeviceRequest get:
                    var json = await client.PostAsync<JToken>(
                        "SignatureCapture/GetSignature",
                        new { deviceName = _deviceName, token = _token, formName = get.FormName }).ConfigureAwait(false);
                    var results = new SignatureCaptureResults
                    {
                        Signature = TokenString(json, "signature", "Signature", "signatureData", "SignatureData"),
                    };
                    return new GetSignatureSignatureCaptureDeviceResponse(results);

                case StopCaptureSignatureCaptureDeviceRequest stop:
                    await client.PostAsync("SignatureCapture/CancelOperation", new { deviceName = _deviceName, token = _token }).ConfigureAwait(false);
                    return NullResponse.Instance;

                case CloseSignatureCaptureDeviceRequest close:
                    await client.PostAsync("SignatureCapture/Unlock", new { deviceName = _deviceName, token = _token }).ConfigureAwait(false);
                    _token = null;
                    return NullResponse.Instance;

                default:
                    throw new NotSupportedException($"Request '{request.GetType().FullName}' is not supported by {HandlerName} signature capture.");
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
