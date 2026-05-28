using System;
using System.ComponentModel;
using System.Windows.Input;
using Cine.Media.Interfaces;
using Cine.Media.Models;

namespace Cine.Avalonia.ViewModels
{
    /// <summary>
    /// Represents a single track item in a track selection menu (subtitle, audio, or video).
    /// Provides display text, enabled state, visual properties, and a command to select the track.
    /// Matches Python's track menu entries including "None" and "Add Track..." pseudo-entries.
    /// </summary>
    public class TrackMenuItem : INotifyPropertyChanged
    {
        private readonly Action<TrackMenuItem>? _selectAction;
        private bool _isSelected;
        private const double PseudoOpacity = 0.6;
        private const double NormalOpacity = 1.0;

        /// <summary>Display label shown in the menu flyout.</summary>
        public string DisplayName { get; }

        /// <summary>Underlying source this menu item represents. Null for pseudo-entries ("None", "Add...").</summary>
        public SubtitleSource? Source { get; }

        /// <summary>Track type this item belongs to.</summary>
        public TrackType TrackType { get; }

        /// <summary>Index of this track in the player's track list. -1 for pseudo-entries.</summary>
        public int TrackIndex { get; }

        /// <summary>Whether this track is currently enabled/active.</summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                    OnPropertyChanged(nameof(DisplayOpacity));
                }
            }
        }

        /// <summary>Opacity for visual distinction: pseudo-entries are dimmed, selected items are full opacity.</summary>
        public double DisplayOpacity => IsSelected ? 1.0 : (IsPseudoEntry ? PseudoOpacity : NormalOpacity);

        /// <summary>Command to execute when this item is clicked.</summary>
        public ICommand SelectCommand { get; }

        /// <summary>True if this is a pseudo-entry like "None" or "Add Subtitle Track".</summary>
        public bool IsPseudoEntry => TrackIndex < 0;

        public TrackMenuItem(string displayName, TrackType trackType, int trackIndex, Action<TrackMenuItem>? selectAction = null, SubtitleSource? source = null)
        {
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            TrackType = trackType;
            TrackIndex = trackIndex;
            _selectAction = selectAction;
            Source = source;
            SelectCommand = new RelayCommand(OnSelect);
        }

        private void OnSelect()
        {
            _selectAction?.Invoke(this);
        }

        public void RefreshSelection(bool isEnabled)
        {
            IsSelected = isEnabled;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public enum TrackType
    {
        Subtitle,
        Audio,
        Video
    }
}