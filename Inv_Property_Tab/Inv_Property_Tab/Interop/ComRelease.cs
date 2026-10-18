using System.Runtime.InteropServices;

namespace Inv_Property_Tab.Interop
{
    public static class ComRelease
    {
        public static void Final(object? value)
        {
            if (value != null && Marshal.IsComObject(value))
                Marshal.FinalReleaseComObject(value);
        }
    }
}
