// Copyright (c) 2026 jojogaming93
// SPDX-License-Identifier: AGPL-3.0-only

using System.Text.Json;



namespace PhoneControl.Domain.Detection;

// Visual HUD classification only. No game memory or gameplay decisions.
public sealed class DrivingHudDetector
{
    public sealed record Feature(double X, double Y, double Width, double Height, double[] Values);
    private readonly Feature[] features;
    private bool driving;
    private int misses;
    public DrivingHudDetector()
    {
        using var data = typeof(DrivingHudDetector).Assembly.GetManifestResourceStream("PhoneControl.DrivingHud.json")!;
        features = JsonSerializer.Deserialize<Feature[]>(data)!;
    }
    public bool IsDrivingFrame(byte[] bgra, int width, int height)
    {
        if (width <= height || width < 400 || bgra.Length != (long)width * height * 4) return false;
        int matched = 0;
        foreach (var f in features) {
            double sumA = 0, sumB = 0, aa = 0, bb = 0, ab = 0;
            for (int y = 0; y < 24; y++) for (int x = 0; x < 24; x++) {
                int px = Math.Clamp((int)Math.Round((f.X + f.Width * (x + .5) / 24) * width), 0, width - 1);
                int py = Math.Clamp((int)Math.Round((f.Y + f.Height * (y + .5) / 24) * height), 0, height - 1);
                int offset = (py * width + px) * 4;
                double a = (bgra[offset] + bgra[offset + 1] + bgra[offset + 2]) / 3d, b = f.Values[y * 24 + x];
                sumA += a; sumB += b; aa += a * a; bb += b * b; ab += a * b;
            }
            double varianceA = aa - sumA * sumA / 576, varianceB = bb - sumB * sumB / 576;
            // Correlation tolerates a dimmed HUD; the variance floor rejects
            // blank frames and the independent arrow shapes reject walking HUDs.
            double score = (ab - sumA * sumB / 576) / Math.Sqrt(Math.Max(1, varianceA * varianceB));
            if (varianceA / 576 > 20 && score >= .72) matched++;
        }
        return matched >= 3;
    }
    public bool Observe(bool present)
    {
        // Three independent arrow matches already provide strong entry evidence.
        // Enter on the first positive frame; tolerate one transient missing HUD.
        if (present) { misses = 0; driving = true; }
        else { misses = Math.Min(2, misses + 1); if (misses == 2) driving = false; }
        return driving;
    }
}
