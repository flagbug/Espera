using System;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Espera.View.ViewModels;
using ReactiveUI;

namespace Espera.View.Views
{
    public partial class YoutubeView : IViewFor<YoutubeViewModel>
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(YoutubeViewModel), typeof(YoutubeView));

        public YoutubeView()
        {
            InitializeComponent();

            this.Events().DataContextChanged.Subscribe(x => this.ViewModel = (YoutubeViewModel)x.NewValue);

            this.YoutubeSongs.ItemContainerStyle.RegisterEventSetter<MouseEventArgs>(MouseMoveEvent, x => new MouseEventHandler(x))
                .Where(x => x.Item2.LeftButton == MouseButtonState.Pressed)
                .Subscribe(x =>
                {
                    x.Item2.Handled = true;
                    DragDrop.DoDragDrop((ListViewItem)x.Item1, DragDropHelper.SongSourceFormat, DragDropEffects.Link);
                });

            this.YoutubeSongs.Events().ContextMenuOpening.Where(_ => !this.ViewModel.SelectableSongs.Any())
                .Subscribe(x => x.Handled = true);
        }

        object IViewFor.ViewModel
        {
            get { return ViewModel; }
            set { ViewModel = (YoutubeViewModel)value; }
        }

        public YoutubeViewModel ViewModel
        {
            get { return (YoutubeViewModel)this.GetValue(ViewModelProperty); }
            set { this.SetValue(ViewModelProperty, value); }
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
            IReactiveCommand command = this.ViewModel.DefaultPlaybackCommand;

            if (e.LeftButton == MouseButtonState.Pressed && command.CanExecute(null))
            {
                command.Execute(null);
            }
        }

        private void SongKeyPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                IReactiveCommand command = this.ViewModel.DefaultPlaybackCommand;

                if (command.CanExecute(null))
                {
                    command.Execute(null);
                }

                e.Handled = true;
            }
        }
    }
}