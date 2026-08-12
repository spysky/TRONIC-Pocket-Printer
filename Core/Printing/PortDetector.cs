using System.Management;
using System.IO.Ports;
using System.Text.RegularExpressions;

namespace TronicPocketPrinter.Core.Printing;

/// <summary>Information about an available COM port.</summary>
public sealed class ComPortInfo
{
    public required string PortName { get; init; }
    public string? Description { get; init; }
    public string? PnpDeviceId { get; init; }
    public bool IsLikelyPrinter { get; init; }

    public string Display =>
        string.IsNullOrEmpty(Description) ? PortName : $"{PortName} — {Description}";

    public override string ToString() => Display;
}

/// <summary>
/// Detects the TRONIC printer's Bluetooth SPP COM port by matching the MAC
/// address inside the PnP Instance ID.
/// </summary>
public static class PortDetector
{
    /// <summary>
    /// Enumerates available serial ports with descriptions via WMI.
    /// </summary>
    public static IReadOnlyList<ComPortInfo> ListPorts(string expectedMac)
    {
        string macDigits = new string(expectedMac.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
        var results = new List<ComPortInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Caption, DeviceID, PNPDeviceID FROM Win32_PnPEntity WHERE Name LIKE '%(COM%'");
            foreach (ManagementObject device in searcher.Get())
            {
                string? name = device["Name"]?.ToString();
                string? pnp = device["PNPDeviceID"]?.ToString();
                if (name is null) continue;

                var match = Regex.Match(name, @"\((COM\d+)\)");
                if (!match.Success) continue;

                string port = match.Groups[1].Value;
                string desc = Regex.Replace(name, @"\s*\(COM\d+\)", "").Trim();
                bool isPrinter = pnp is not null && macDigits.Length > 0 &&
                                 pnp.ToUpperInvariant().Contains(macDigits);

                results.Add(new ComPortInfo
                {
                    PortName = port,
                    Description = desc,
                    PnpDeviceId = pnp,
                    IsLikelyPrinter = isPrinter
                });
                seen.Add(port);
            }
        }
        catch
        {
            // WMI failure: fall back to plain port names below.
        }

        // Ensure any port SerialPort sees is present even if WMI missed it.
        foreach (var p in SerialPort.GetPortNames())
        {
            if (seen.Add(p))
            {
                results.Add(new ComPortInfo { PortName = p });
            }
        }

        return results
            .OrderByDescending(p => p.IsLikelyPrinter)
            .ThenBy(p => p.PortName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Returns the detected printer port, or null if not found.
    /// </summary>
    public static ComPortInfo? DetectPrinterPort(string expectedMac)
        => ListPorts(expectedMac).FirstOrDefault(p => p.IsLikelyPrinter);
}
