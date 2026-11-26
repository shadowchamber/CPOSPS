using System.Reflection;

namespace IIGCore.HardwareStation.PosTerminalWeb.Handlers
{
    public static class PtwDeviceName
    {
        public static string From(object request, string fallback = null)
        {
            if (request == null)
            {
                return fallback;
            }

            var type = request.GetType();
            while (type != null)
            {
                var prop = type.GetProperty("DeviceName", BindingFlags.Public | BindingFlags.Instance);
                if (prop != null && prop.PropertyType == typeof(string))
                {
                    var value = prop.GetValue(request) as string;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }

                type = type.BaseType;
            }

            return fallback;
        }

        public static object BodyWithDevice(string deviceName, object extra = null)
        {
            if (extra == null)
            {
                return new { deviceName = deviceName ?? string.Empty };
            }

            // Merge via dictionary for open requests that only need deviceName.
            var dict = new System.Collections.Generic.Dictionary<string, object>(System.StringComparer.OrdinalIgnoreCase)
            {
                ["deviceName"] = deviceName ?? string.Empty,
            };

            foreach (var prop in extra.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                dict[prop.Name] = prop.GetValue(extra);
            }

            return dict;
        }
    }
}
