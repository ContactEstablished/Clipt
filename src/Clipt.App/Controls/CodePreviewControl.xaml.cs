using System.Windows;
using System.Windows.Controls;
using ICSharpCode.AvalonEdit.Highlighting;

namespace Clipt.App.Controls;

public partial class CodePreviewControl : UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
        nameof(Text),
        typeof(string),
        typeof(CodePreviewControl),
        new PropertyMetadata(string.Empty, OnPreviewChanged));

    public static readonly DependencyProperty SyntaxLanguageProperty = DependencyProperty.Register(
        nameof(SyntaxLanguage),
        typeof(string),
        typeof(CodePreviewControl),
        new PropertyMetadata(string.Empty, OnPreviewChanged));

    public CodePreviewControl()
    {
        InitializeComponent();
        UpdateEditor();
    }

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string SyntaxLanguage
    {
        get => (string)GetValue(SyntaxLanguageProperty);
        set => SetValue(SyntaxLanguageProperty, value);
    }

    private static void OnPreviewChanged(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs e)
    {
        if (dependencyObject is CodePreviewControl control)
        {
            control.UpdateEditor();
        }
    }

    private void UpdateEditor()
    {
        Editor.Text = Text ?? string.Empty;

        var definitionName = SyntaxLanguage switch
        {
            "C#" => "C#",
            "JSON" => "JavaScript",
            "SQL" => "SQL",
            _ => "C#",
        };

        Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition(definitionName);
    }
}
