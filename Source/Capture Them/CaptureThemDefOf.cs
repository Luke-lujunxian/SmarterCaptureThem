using RimWorld;
using Verse;

namespace SmartCaptureThem;

[DefOf]
public static class CaptureThemDefOf
{
    public static DesignationDef CaptureThemCapture;
    public static DesignationDef CaptureThemCapture_FirstAid;
    public static DesignationDef CaptureThemCapture_CE;

    static CaptureThemDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(CaptureThemDefOf));
    }
}