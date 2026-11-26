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
    /// Sole OPOS-chain bridge for one peripheral kind: PTW* → PosTerminal.Web handler,
    /// MockOPOS* → ExecuteNext(stock). Never register a second HandlerName=OPOS handler
    /// for the same SupportedRequestTypes — ExecuteNext adapter SkipWhile stack-overflows IIS.
    /// </summary>
    public abstract class CustomOposPtwBridgeDevice : INamedRequestHandlerAsync
    {
        private readonly string kind;
        private readonly INamedRequestHandlerAsync ptwHandler;

        protected CustomOposPtwBridgeDevice(string kind, INamedRequestHandlerAsync ptwHandler)
        {
            this.kind = kind ?? throw new ArgumentNullException(nameof(kind));
            this.ptwHandler = ptwHandler ?? throw new ArgumentNullException(nameof(ptwHandler));
        }

        public string HandlerName => PeripheralType.Opos;

        public IEnumerable<Type> SupportedRequestTypes => this.ptwHandler.SupportedRequestTypes;

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));

            // Peek BEFORE TrackSession — Close/Unlock clears ActiveByKind.
            var activeBefore = PtwOposNameBridge.GetActive(this.kind);
            var isPtw = PtwOposNameBridge.IsPtwName(activeBefore)
                || PtwOposNameBridge.IsPtwDevice(request, this.kind);

            PtwOposNameBridge.TrackSession(request, this.kind);

            if (isPtw)
            {
                return await this.ptwHandler.Execute(request).ConfigureAwait(false);
            }

            // Close/Unlock with no MockOPOS session: never ExecuteNext (0xc00000fd).
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
    }
}
