using IIGCore.HardwareStation.PosTerminalWeb.Handlers;

namespace IIGCore.HardwareStation.PosTerminalWeb.Peripherals
{
    /// <summary>Sole OPOS MSR bridge. PTW* → SIM; MockOPOS* → stock.</summary>
    public sealed class CustomOposMagneticStripeReaderDevice : CustomOposPtwBridgeDevice
    {
        public CustomOposMagneticStripeReaderDevice()
            : base("Msr", new PtwMagneticStripeReaderDevice())
        {
        }
    }
}
