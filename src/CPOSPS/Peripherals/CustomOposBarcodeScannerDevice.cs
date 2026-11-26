using IIGCore.HardwareStation.PosTerminalWeb.Handlers;

namespace IIGCore.HardwareStation.PosTerminalWeb.Peripherals
{
    /// <summary>Sole OPOS barcode-scanner bridge. PTW* → SIM; MockOPOS* → stock.</summary>
    public sealed class CustomOposBarcodeScannerDevice : CustomOposPtwBridgeDevice
    {
        public CustomOposBarcodeScannerDevice()
            : base("BarcodeScanner", new PtwBarcodeScannerDevice())
        {
        }
    }
}
