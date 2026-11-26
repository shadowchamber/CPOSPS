namespace IIGCore.HardwareStation.PosTerminalWeb
{
    /// <summary>
    /// Handler name and logical device names for the PosTerminal.Web bridge.
    /// HandlerName must match the DeviceType string POS sends to Hardware Station.
    /// </summary>
    public static class PosTerminalWebConstants
    {
        public const string HandlerName = "PTW";

        public const string Scanner = "PTWScanner";
        public const string Drawer1 = "PTWDrawer1";
        public const string Drawer2 = "PTWDrawer2";
        public const string Msr = "PTWMSR";
        public const string PinPad = "PTWPinPad";
        public const string Printer = "PTWPrinter";
        public const string Scale = "PTWScale";
        public const string SignatureCapture = "PTWSignatureCapture";
        public const string LineDisplay = "PTWLineDisplay";
        public const string PaymentTerminal = "PTWPAYMENTTERMINAL";
        public const string Light = "PTWLight";
        public const string CoinDispenser = "PTWCoinDispenser";
        public const string RemoteOrderDisplay = "PTWRemoteOrderDisplay";

        public static readonly string[] DeviceNames = new[]
        {
            Scanner,
            Drawer1,
            Drawer2,
            Msr,
            PinPad,
            Printer,
            Scale,
            SignatureCapture,
            LineDisplay,
            PaymentTerminal,
            Light,
            CoinDispenser,
            RemoteOrderDisplay,
        };
    }
}
