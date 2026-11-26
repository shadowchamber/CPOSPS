using IIGCore.HardwareStation.PosTerminalWeb.Handlers;

namespace IIGCore.HardwareStation.PosTerminalWeb.Peripherals
{
    /// <summary>Sole OPOS scale bridge. PTW* → SIM; MockOPOS* → stock.</summary>
    public sealed class CustomOposScaleDevice : CustomOposPtwBridgeDevice
    {
        public CustomOposScaleDevice()
            : base("Scale", new PtwScaleDevice())
        {
        }
    }
}
