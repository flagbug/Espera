using Espera.View.ViewModels;

namespace Espera.View.Views
{
    public partial class YoutubeView : IViewFor<YoutubeViewModel>
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(YoutubeViewModel), typeof(YoutubeView));

        public YoutubeView()
        {
            InitializeComponent();

            this.Events().DataContextChanged.Subscribe(x => ViewModel = (YoutubeViewModel)x.NewValue);

            YoutubeSongs.ItemContainerStyle
                .RegisterEventSetter<MouseEventArgs>(MouseMoveEvent, x => new MouseEventHandler(x))
                .Where(x => x.Item2.LeftButton == MouseButtonState.Pressed)
                .Subscribe(x =>
                {
                    x.Item2.Handled = true;
                    DragDrop.DoDragDrop((ListViewItem)x.Item1, DragDropHelper.SongSourceFormat, DragDropEffects.Link);
                });

            YoutubeSongs.Events().ContextMenuOpening.Where(_ => !ViewModel.SelectableSongs.Any())
                .Subscribe(x => x.Handled = true);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (YoutubeViewModel)value;
        }

        public YoutubeViewModel ViewModel
        {
            get => (YoutubeViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
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
            var command = ViewModel.DefaultPlaybackCommand;

            if (e.LeftButton == MouseButtonState.Pressed && command.CanExecute(null)) command.Execute(null);
        }

        private void SongKeyPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var command = ViewModel.DefaultPlaybackCommand;

                if (command.CanExecute(null)) command.Execute(null);

                e.Handled = true;
            }
        }
    }
}