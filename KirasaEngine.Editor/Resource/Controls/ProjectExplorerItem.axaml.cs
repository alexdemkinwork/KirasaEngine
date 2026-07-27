using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace KirasaEngine.Editor.Resource.Controls
{
    public partial class ProjectExplorerItem : UserControl
    {
        public IconType StatusChangeIcon;
        public IconType ItemIcon { get; set; } = IconType.None;
        public string Text { get; set; }
        public ProjectExplorerItem()
        {
            InitializeComponent();
        }
    }
}