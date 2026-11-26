using IIGCore.HardwareStation.PosTerminalWeb.Handlers;
using Microsoft.Dynamics.Commerce.HardwareStation;
using Microsoft.Dynamics.Commerce.HardwareStation.PeripheralRequests;
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
    /// Sole OPOS cash-drawer extension handler. PTW* → PosTerminal.Web; MockOPOS* → ExecuteNext(stock).
    /// Do not add a second HandlerName=OPOS cash-drawer bridge — ExecuteNext adapter SkipWhile
    /// ping-pongs and stack-overflows IIS (0xc00000fd).
    /// </summary>
    public sealed class CustomOposCashDrawerDevice : INamedRequestHandlerAsync
    {
        private const string Kind = "CashDrawer";
        private static readonly PtwCashDrawerDevice PtwDrawer = new PtwCashDrawerDevice();

        public string HandlerName => PeripheralType.Opos;

        public IEnumerable<Type> SupportedRequestTypes => PtwDrawer.SupportedRequestTypes;

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));

            // Peek BEFORE TrackSession. Close/Unlock clears ActiveByKind; checking after makes
            // Close look non-PTW and ExecuteNext(stock OPOS) → 0xc00000fd (observed as hundreds
            // of CloseCashDrawerDeviceRequest lines then w3wp fault during CreateShift).
            var activeBefore = PtwOposNameBridge.GetActive(Kind);
            var isPtw = PtwOposNameBridge.IsPtwName(activeBefore)
                || PtwOposNameBridge.IsPtwDevice(request, Kind);

            PtwOposNameBridge.TrackSession(request, Kind);

            try
            {
                System.IO.File.AppendAllText(
                    @"C:\Temp\hws-drawer.log",
                    DateTime.UtcNow.ToString("o") + " " + request.GetType().Name
                    + " isPtw=" + isPtw
                    + " activeBefore=" + (activeBefore ?? "")
                    + " activeAfter=" + (PtwOposNameBridge.GetActive(Kind) ?? "")
                    + Environment.NewLine);
            }
            catch
            {
                // ignore probe logging failures
            }

            if (isPtw)
            {
                return await PtwDrawer.Execute(request).ConfigureAwait(false);
            }

            // Close with no MockOPOS session: never ExecuteNext — empty/stock Close SO-crashes IIS.
            if (request is CloseCashDrawerDeviceRequest)
            {
                return NullResponse.Instance;
            }

            return await request.RequestContext.Runtime.ExecuteNextAsync<Response>(
                this, request, request.RequestContext, true, HandlerName).ConfigureAwait(false);
        }
    }
}
