using System;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Espera.View.ViewModels;
using ReactiveUI;

namespace Espera.View.Views
{
    public partial class LocalView : IViewFor<LocalViewModel>
    {
        public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register("ViewModel", typeof(LocalViewModel), typeof(LocalView));

        public LocalView()
        {
            InitializeComponent();

            this.Events().DataContextChanged.Subscribe(x => this.ViewModel = (LocalViewModel)x.NewValue);

            this.LocalSongs.ItemContainerStyle.RegisterEventSetter<MouseEventArgs>(MouseMoveEvent, x => new MouseEventHandler(x))
                .Where(x => x.Item2.LeftButton == MouseButtonState.Pressed)
                .Subscribe(x =>
                {
                    x.Item2.Handled = true;
                    DragDrop.DoDragDrop((ListViewItem)x.Item1, DragDropHelper.SongSourceFormat, DragDropEffects.Link);
                });

            this.LocalSongs.Events().ContextMenuOpening.Where(_ => !this.ViewModel.SelectableSongs.Any())
                .Subscribe(x => x.Handled = true);
        }

        object IViewFor.ViewModel
        {
            get { return ViewModel; }
            set { ViewModel = (LocalViewModel)value; }
        }

        public LocalViewModel ViewModel
        {
            get { return (LocalViewModel)this.GetValue(ViewModelProperty); }
            set { this.SetValue(ViewModelProperty, value); }
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