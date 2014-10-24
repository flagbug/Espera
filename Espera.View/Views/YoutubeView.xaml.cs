using System;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Espera.View.ViewModels;

namespace Espera.View.Views
{
    /// <summary>
    /// Interaction logic for YoutubeView.xaml
    /// </summary>
    public partial class YoutubeView
    {
        public YoutubeView()
        {
            InitializeComponent();

            this.YoutubeSongs.ItemContainerStyle.RegisterEventSetter<MouseEventArgs>(MouseMoveEvent, x => new MouseEventHandler(x))
                .Where(x => x.Item2.LeftButton == MouseButtonState.Pressed)
                .Subscribe(x =>
                {
                    x.Item2.Handled = true;
                    DragDrop.DoDragDrop((ListViewItem)x.Item1, DragDropHelper.SongSourceFormat, DragDropEffects.Link);
                });
        }

        private void ExternalPathLeftMouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
        }

        private void ExternalPathLeftMouseButtonUp(object sender, MouseButtonEventArgs e)
        {
            ((SongViewModelBase)((Hyperlink)sender).DataContext).OpenPathCommand.Execute(null);

            e.Handled = true;
        }

        private void SongDoubleClick(object sender, MouseButtonEventArgs e)
        {
            ICommand command = ((YoutubeViewModel)this.DataContext).DefaultPlaybackCommand;

            if (e.LeftButton == MouseButtonState.Pressed && command.CanExecute(null))
            {
                command.Execute(null);
            }
        }

        private void SongKeyPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ICommand command = ((YoutubeViewModel)this.DataContext).DefaultPlaybackCommand;

                if (command.CanExecute(null))
                {
                    command.Execute(null);
                }

                e.Handled = true;
            }
        }

        private void SongListContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (((ListView)sender).Items.IsEmpty)
            {
                e.Handled = true;
            }
        }
    }
}