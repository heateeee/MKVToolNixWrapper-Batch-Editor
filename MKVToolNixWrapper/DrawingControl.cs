using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;

namespace MKVToolNixWrapper;

public class cDrawingControl
{
    private const int WM_SETREDRAW = 0x000B;

    public static void SuspendDrawing(IntPtr handle)
    {
        _ = SendMessage(handle, WM_SETREDRAW, 0, IntPtr.Zero);
    }

    /// <summary>
    /// This resumes the drawing for the UIElement.
    /// <br>Control is preferable to be present,</br>
    /// <br>as it's instantly updating this control when drawing is enabled again!</br>
    /// </summary>
    /// <param name="element"></param>
    /// <param name="redraw"></param>
    public static void ResumeDrawing(UIElement element, bool redraw = false)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));

        var handle = ((HwndSource)PresentationSource.FromVisual(element)).Handle;
        _ = SendMessage(handle, WM_SETREDRAW, 1, IntPtr.Zero);

        if (redraw)        
            element.InvalidateVisual();        
    }

    public static void ResumeDrawing(IntPtr handle)
    {
        _ = SendMessage(handle, WM_SETREDRAW, 1, IntPtr.Zero);
    }

    [DllImport("user32.dll")]
    private static extern int SendMessage(IntPtr hWnd, int wMsg, int wParam, IntPtr lParam);

    public static IntPtr GetWindowHandle(Window window)
    {
        WindowInteropHelper helper = new WindowInteropHelper(window);
        return helper.Handle;
    }
}
