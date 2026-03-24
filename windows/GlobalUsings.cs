// Disambiguate WPF and WinForms types that share names.
// All WPF aliases listed here resolve to the WPF type; WinForms-specific code
// in TrayManager.cs uses fully-qualified names or its own explicit usings.
global using Application        = System.Windows.Application;
global using TextBox             = System.Windows.Controls.TextBox;
global using KeyEventArgs        = System.Windows.Input.KeyEventArgs;
global using Brush               = System.Windows.Media.Brush;
global using Brushes             = System.Windows.Media.Brushes;
global using Color               = System.Windows.Media.Color;
global using Cursors             = System.Windows.Input.Cursors;
global using FontFamily          = System.Windows.Media.FontFamily;
global using HorizontalAlignment = System.Windows.HorizontalAlignment;
