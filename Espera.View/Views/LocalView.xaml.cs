using Espera.View.ViewModels;

namespace Espera.View.Views
{
    public partial class LocalView : IViewFor<LocalViewModel>
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(LocalViewModel), typeof(LocalView));

        public LocalView()
        {
            InitializeComponent();

            this.Events().DataContextChanged.Subscribe(x => ViewModel = (LocalViewModel)x.NewValue);

            LocalSongs.ItemContainerStyle
                .RegisterEventSetter<MouseEventArgs>(MouseMoveEvent, x => new MouseEventHandler(x))
                .Where(x => x.Item2.LeftButton == MouseButtonState.Pressed)
                .Subscribe(x =>
                {
                    x.Item2.Handled = true;
                    DragDrop.DoDragDrop((ListViewItem)x.Item1, DragDropHelper.SongSourceFormat, DragDropEffects.Link);
                });

            LocalSongs.Events().ContextMenuOpening.Where(_ => !ViewModel.SelectableSongs.Any())
                .Subscribe(x => x.Handled = true);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (LocalViewModel)value;
        }

        public LocalViewModel ViewModel
        {
            get => (LocalViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
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