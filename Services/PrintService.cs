using System.Drawing;
using System.Drawing.Printing;

namespace FFPOS.Services;

public class PrintService
{
    public void PrintReceipt(string text, string? printerName = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        using var document = new PrintDocument();
        if (!string.IsNullOrWhiteSpace(printerName))
        {
            document.PrinterSettings.PrinterName = printerName;
        }

        document.PrintPage += (_, e) =>
        {
            using var font = new Font("Consolas", 10);
            e.Graphics?.DrawString(text, font, Brushes.Black, new RectangleF(8, 8, e.MarginBounds.Width, e.MarginBounds.Height));
        };

        document.Print();
    }
}
