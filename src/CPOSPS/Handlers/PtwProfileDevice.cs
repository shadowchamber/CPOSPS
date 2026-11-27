using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Dynamics.Commerce.HardwareStation.Peripherals;
using Microsoft.Dynamics.Commerce.Runtime;
using Microsoft.Dynamics.Commerce.Runtime.Handlers;
using Microsoft.Dynamics.Commerce.Runtime.Messages;
using ThrowIf = Microsoft.Dynamics.Commerce.HardwareStation.ThrowIf;

namespace IIGCore.HardwareStation.PosTerminalWeb.Handlers
{
    /// <summary>
    /// Virtual PTW peripheral host. Hardware profile keeps stock type OPOS with device names PTW*;
    /// this host claims those names and drives the in-process simulator. Non-PTW names fall through
    /// to stock OPOS. No ServiceOPOS registry and no real devices.
    /// </summary>
    public abstract class PtwProfileDevice : INamedRequestHandlerAsync
    {
        private readonly string kind;
        private readonly INamedRequestHandlerAsync virtualDevice;

        protected PtwProfileDevice(string kind, INamedRequestHandlerAsync virtualDevice)
        {
            this.kind = kind ?? throw new ArgumentNullException(nameof(kind));
            this.virtualDevice = virtualDevice ?? throw new ArgumentNullException(nameof(virtualDevice));
        }

        public string HandlerName => PeripheralType.Opos;

        public IEnumerable<Type> SupportedRequestTypes => this.virtualDevice.SupportedRequestTypes;

        public async Task<Response> Execute(Request request)
        {
            ThrowIf.Null(request, nameof(request));

            var activeBefore = PtwOposNameBridge.GetActive(this.kind);
            var isPtw = PtwOposNameBridge.IsPtwName(activeBefore)
                || PtwOposNameBridge.IsPtwDevice(request, this.kind);

            PtwOposNameBridge.TrackSession(request, this.kind);

            if (isPtw)
            {
                return await this.virtualDevice.Execute(request).ConfigureAwait(false);
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
    }

    public sealed class PtwProfilePrinterDevice : PtwProfileDevice
    {
        public PtwProfilePrinterDevice() : base("Printer", new PtwPrinterDevice()) { }
    }

    public sealed class PtwProfileCashDrawerDevice : PtwProfileDevice
    {
        public PtwProfileCashDrawerDevice() : base("CashDrawer", new PtwCashDrawerDevice()) { }
    }

    public sealed class PtwProfileBarcodeScannerDevice : PtwProfileDevice
    {
        public PtwProfileBarcodeScannerDevice() : base("BarcodeScanner", new PtwBarcodeScannerDevice()) { }
    }

    public sealed class PtwProfileLineDisplayDevice : PtwProfileDevice
    {
        public PtwProfileLineDisplayDevice() : base("LineDisplay", new PtwLineDisplayDevice()) { }
    }

    public sealed class PtwProfileMagneticStripeReaderDevice : PtwProfileDevice
    {
        public PtwProfileMagneticStripeReaderDevice() : base("MSR", new PtwMagneticStripeReaderDevice()) { }
    }

    public sealed class PtwProfilePinPadDevice : PtwProfileDevice
    {
        public PtwProfilePinPadDevice() : base("PinPad", new PtwPinPadDevice()) { }
    }

    public sealed class PtwProfileScaleDevice : PtwProfileDevice
    {
        public PtwProfileScaleDevice() : base("Scale", new PtwScaleDevice()) { }
    }

    public sealed class PtwProfileSignatureCaptureDevice : PtwProfileDevice
    {
        public PtwProfileSignatureCaptureDevice() : base("SignatureCapture", new PtwSignatureCaptureDevice()) { }
    }
}
