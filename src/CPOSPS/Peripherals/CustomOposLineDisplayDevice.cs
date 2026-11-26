using IIGCore.HardwareStation.PosTerminalWeb.Handlers;

namespace IIGCore.HardwareStation.PosTerminalWeb.Peripherals
{
    /// <summary>Sole OPOS line-display bridge. PTW* → SIM; MockOPOS* → stock.</summary>
    public sealed class CustomOposLineDisplayDevice : CustomOposPtwBridgeDevice
    {
        public CustomOposLineDisplayDevice()
            : base("LineDisplay", new PtwLineDisplayDevice())
        {
        }
    }
}
