using System.Windows;
using System.Windows.Input;

namespace FFPOS.Views;

public partial class OrderNoteWindow : Window
{
    public OrderNoteWindow(string? note)
    {
        InitializeComponent();
        NoteBox.Text = note ?? string.Empty;
        Loaded += (_, _) =>
        {
            NoteBox.Focus();
            NoteBox.CaretIndex = NoteBox.Text.Length;
        };
    }

    public string Note => NoteBox.Text.Trim();

    private void SaveClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelClicked(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void HeaderMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }
}
