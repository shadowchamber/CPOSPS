using IIGCore.HardwareStation.PosTerminalWeb.Handlers;

namespace IIGCore.HardwareStation.PosTerminalWeb.Peripherals
{
    /// <summary>Sole OPOS signature-capture bridge. PTW* → SIM; MockOPOS* → stock.</summary>
    public sealed class CustomOposSignatureCaptureDevice : CustomOposPtwBridgeDevice
    {
        public CustomOposSignatureCaptureDevice()
            : base("SignatureCapture", new PtwSignatureCaptureDevice())
        {
        }
    }
}
