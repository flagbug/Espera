﻿using System;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using Espera.View.ViewModels;
using ReactiveUI;
using EventsMixin = System.Windows.EventsMixin;

namespace Espera.View.Views
{
    public partial class YoutubeView : IViewFor<YoutubeViewModel>
    {
        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(YoutubeViewModel), typeof(YoutubeView));

        public YoutubeView()
        {
            InitializeComponent();

            EventsMixin.Events(this).DataContextChanged.Subscribe(x => ViewModel = (YoutubeViewModel)x.NewValue);

            YoutubeSongs.ItemContainerStyle
                .RegisterEventSetter<MouseEventArgs>(MouseMoveEvent, x => new MouseEventHandler(x))
                .Where(x => x.Item2.LeftButton == MouseButtonState.Pressed)
                .Subscribe(x =>
                {
                    x.Item2.Handled = true;
                    DragDrop.DoDragDrop((ListViewItem)x.Item1, DragDropHelper.SongSourceFormat, DragDropEffects.Link);
                });

            EventsMixin.Events(YoutubeSongs).ContextMenuOpening.Where(_ => !ViewModel.SelectableSongs.Any())
                .Subscribe(x => x.Handled = true);
        }

        object IViewFor.ViewModel
        {
            get => ViewModel;
            set => ViewModel = (YoutubeViewModel)value;
        }

        public YoutubeViewModel ViewModel
        {
            get => (YoutubeViewModel)this.GetValue(ViewModelProperty);
            set => this.SetValue(ViewModelProperty, value);
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
            IReactiveCommand command = ViewModel.DefaultPlaybackCommand;

            if (e.LeftButton == MouseButtonState.Pressed && command.CanExecute(null)) command.Execute(null);
        }

        private void SongKeyPressed(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                IReactiveCommand command = ViewModel.DefaultPlaybackCommand;

                if (command.CanExecute(null)) command.Execute(null);

                e.Handled = true;
            }
        }
    }
}