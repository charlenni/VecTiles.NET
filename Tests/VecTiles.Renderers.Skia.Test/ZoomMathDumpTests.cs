using Xunit;

namespace VecTiles.Renderers.Skia.Test;

public class ZoomMathDumpTests
{
    [Fact]
    public void DumpZoomMath()
    {
        string FindDeclaring(string methodName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (!asm.IsDynamic)
                {
                    Type?[] types;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        foreach (var m in t.GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                        {
                            if (m.Name == methodName) return t.FullName + ", " + asm.GetName().Name;
                        }
                    }
                }
            }
            return "NOT FOUND";
        }

        double resolution = 78271.51696402031 / Math.Pow(2, 19);
        File.WriteAllText("/tmp/zoommath.txt",
            $"decl ToZoomLevel: {FindDeclaring("ToZoomLevel")}\n" +
            $"decl ToResolution: {FindDeclaring("ToResolution")}\n" +
            $"resolution={resolution:R} bits=0x{BitConverter.DoubleToInt64Bits(resolution):X16}\n");
    }
}
