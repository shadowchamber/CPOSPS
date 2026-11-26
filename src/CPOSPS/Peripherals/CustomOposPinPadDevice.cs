using IIGCore.HardwareStation.PosTerminalWeb;
using IIGCore.HardwareStation.PosTerminalWeb.Handlers;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.HardwareStation.Peripherals;
using Microsoft.Dynamics.Commerce.Runtime;
using Microsoft.Dynamics.Commerce.Runtime.Handlers;
using Microsoft.Dynamics.Commerce.Runtime.Messages;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThrowIf = Microsoft.Dynamics.Commerce.HardwareStation.ThrowIf;

namespace IIGCore.HardwareStation.PosTerminalWeb.Peripherals
{
    /// <summary>
    /// Sole OPOS PIN-pad bridge.
    /// PTWPinPad → SIM PinPad/*; PTWPAYMENTTERMINAL → no-op (payment uses named HandlerName);
    /// MockOPOS* → ExecuteNext(stock). Never ExecuteNext for payment-named devices.
    /// </summary>
    public sealed class CustomOposPinPadDevice : INamedRequestHandlerAsync
    {
        private const string Kind = "PinPad";
        private static readonly PtwPinPadDevice PtwPinPad = new PtwPinPadDevice();

        public string HandlerName => PeripheralType.Opos;

        public IEnumerable<Type> SupportedRequestTypes => PtwPinPad.SupportedRequestTypes;

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));

            var activeBefore = PtwOposNameBridge.GetActive(Kind);
            var name = PtwDeviceName.From(request, activeBefore) ?? activeBefore ?? string.Empty;

            // Payment terminal shares the PINPAD name slot — do not route to PinPad/* or stock OPOS.
            if (IsPaymentTerminalName(name))
            {
                PtwOposNameBridge.TrackSession(request, Kind);
                return NullResponse.Instance;
            }

            var isPtwPinPad = name.StartsWith("PTWPinPad", StringComparison.OrdinalIgnoreCase)
                || (PtwOposNameBridge.IsPtwName(name)
                    && name.IndexOf("PAYMENT", StringComparison.OrdinalIgnoreCase) < 0
                    && name.IndexOf("PinPad", StringComparison.OrdinalIgnoreCase) >= 0);

            var activeIsPtwPinPad = !string.IsNullOrEmpty(activeBefore)
                && activeBefore.StartsWith("PTWPinPad", StringComparison.OrdinalIgnoreCase);

            PtwOposNameBridge.TrackSession(request, Kind);

            if (isPtwPinPad || activeIsPtwPinPad)
            {
                return await PtwPinPad.Execute(request).ConfigureAwait(false);
            }

            var typeName = request.GetType().Name ?? string.Empty;
            if (typeName.StartsWith("Close", StringComparison.Ordinal)
                || typeName.StartsWith("Unlock", StringComparison.Ordinal)
                || typeName.StartsWith("UnLock", StringComparison.Ordinal))
            {
                return NullResponse.Instance;
            }

            return await request.RequestContext.Runtime.ExecuteNextAsync<Response>(
                this, request, request.RequestContext, true, HandlerName).ConfigureAwait(false);
        }

        private static bool IsPaymentTerminalName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return false;
            }

            return name.Equals(PosTerminalWebConstants.PaymentTerminal, StringComparison.OrdinalIgnoreCase)
                || name.IndexOf("PAYMENT", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
