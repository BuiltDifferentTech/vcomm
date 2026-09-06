using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using VComm;
using VComm.Core.Windows;
using Xunit;

namespace VComm.Tests;

public sealed class ThemeSmokeTests
{
    [Fact]
    public void BuilderLoadsDarkEditableComboBoxTemplate()
    {
        Exception? failure = null;
        Thread thread = new Thread(() =>
        {
            try
            {
                App application = new App();
                application.InitializeComponent();
                CreateVPack builder = new CreateVPack();
                builder.Measure(new Size(1120, 760));
                builder.Arrange(new Rect(0, 0, 1120, 760));
                builder.UpdateLayout();

                ComboBox comboBox = Assert.IsType<ComboBox>(builder.FindName("InputPicker"));
                Assert.True(comboBox.ApplyTemplate());
                TextBox editor = Assert.IsType<TextBox>(comboBox.Template.FindName("PART_EditableTextBox", comboBox));
                Assert.Equal(Color.FromRgb(245, 245, 245), ((SolidColorBrush)editor.Foreground).Color);
                Assert.Equal(Color.FromRgb(24, 24, 24), ((SolidColorBrush)comboBox.Background).Color);

                builder.Close();
                application.Shutdown();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(10)), "The WPF theme smoke test timed out.");
        Assert.Null(failure);
    }
}
