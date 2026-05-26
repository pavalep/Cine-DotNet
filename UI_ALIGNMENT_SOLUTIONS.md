# UI Alignment Solutions: Pixel-Perfect Matching

## Overview
This document provides detailed solutions to align the Avalonia implementation with the Python (GTK4) reference implementation. Each solution includes specific implementation steps, code examples, and visual specifications.

## 1. Foundation: Color System & Typography

### Color Palette Implementation
Create a centralized color resource file:

```xml
<!-- Colors.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- Base Colors -->
    <Color x:Key="Black">#000000</Color>
    <Color x:Key="White">#FFFFFF</Color>
    <Color x:Key="Gray100">#E5E5E5</Color>
    <Color x:Key="Gray800">#202021</Color>
    <Color x:Key="Gray900">#19191B</Color>
    
    <!-- OSD (On-Screen Display) Colors -->
    <SolidColorBrush x:Key="OsdForeground" Color="{StaticResource White}" />
    <LinearGradientBrush x:Key="HeaderGradient" StartPoint="0,0" EndPoint="0,1">
        <GradientStop Offset="0" Color="#24000000" />   <!-- rgba(0,0,0,0.14) -->
        <GradientStop Offset="0.4" Color="#14000000" /> <!-- rgba(0,0,0,0.08) -->
        <GradientStop Offset="1" Color="#00000000" />   <!-- transparent -->
    </LinearGradientBrush>
    
    <LinearGradientBrush x:Key="ControlsGradient" StartPoint="0,1" EndPoint="0,0">
        <GradientStop Offset="0" Color="#33000000" />   <!-- rgba(0,0,0,0.2) -->
        <GradientStop Offset="0.4" Color="#1A000000" /> <!-- rgba(0,0,0,0.1) -->
        <GradientStop Offset="1" Color="#00000000" />   <!-- transparent -->
    </LinearGradientBrush>
    
    <!-- Button States -->
    <SolidColorBrush x:Key="ButtonHoverBackground" Color="#2BFFFFFF" /> <!-- rgba(255,255,255,0.17) -->
    <SolidColorBrush x:Key="ButtonActiveBackground" Color="#40FFFFFF" /> <!-- rgba(255,255,255,0.25) -->
    <SolidColorBrush x:Key="ToggleButtonCheckedBackground" Color="{StaticResource White}" />
    
    <!-- Progress/Seek Bar -->
    <SolidColorBrush x:Key="ProgressTroughBackground" Color="#39FFFFFF" /> <!-- rgba(255,255,255,0.225) -->
    <SolidColorBrush x:Key="ProgressSliderBackground" Color="{StaticResource White}" />
    <SolidColorBrush x:Key="TimeSeparatorBackground" Color="#DDDDDD" />
    
</ResourceDictionary>
```

### Typography System
```xml
<!-- Typography.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- Font Families -->
    <FontFamily x:Key="NumericFont">Consolas, Courier New, monospace</FontFamily>
    <FontFamily x:Key="SystemFont">Segoe UI, system-ui, sans-serif</FontFamily>
    
    <!-- Text Styles -->
    <Style Selector="TextBlock.time-label">
        <Setter Property="FontFamily" Value="{StaticResource NumericFont}" />
        <Setter Property="FontSize" Value="13" />
        <Setter Property="Foreground" Value="{StaticResource OsdForeground}" />
        <Setter Property="VerticalAlignment" Value="Center" />
    </Style>
    
    <Style Selector="TextBlock.time-elapsed">
        <Setter Property="Style" Value="{StaticResource time-label}" />
        <Setter Property="Margin" Value="0,0,-7,0" />
    </Style>
    
    <Style Selector="TextBlock.heading">
        <Setter Property="FontWeight" Value="Medium" />
        <Setter Property="Foreground" Value="{StaticResource OsdForeground}" />
    </Style>
    
</ResourceDictionary>
```

## 2. Layout Reconstruction: Overlay-Based Design

### Main Window Structure
Replace current Grid layout with Overlay-based design:

```xml
<!-- MainWindow.axaml -->
<Window xmlns="https://github.com/avaloniaui"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="using:Cine.Avalonia.ViewModels"
        xmlns:conv="using:Cine.Avalonia.Converters"
        xmlns:ctrl="using:Cine.Avalonia.Controls"
        x:Class="Cine.Avalonia.MainWindow"
        x:DataType="vm:MainViewModel"
        Title="Cine"
        Width="800" Height="600"
        MinWidth="332" MinHeight="187"
        Background="Transparent"
        WindowStartupLocation="CenterScreen"
        ExtendClientAreaToDecorationsHint="True"
        TransparencyLevelHint="Blur">

    <Window.Resources>
        <conv:TimeSpanToStringConverter x:Key="TimeSpanToString" />
        <conv:PercentConverter x:Key="PercentConverter" />
    </Window.Resources>

    <!-- Main Overlay Container -->
    <Overlay x:Name="MainOverlay" Background="Black">
        
        <!-- Video Host (Primary Content) -->
        <ctrl:D3D11VideoHost x:Name="VideoHost" />
        
        <!-- Pause Indicator (Overlay) -->
        <Border x:Name="PauseIndicator" 
                HorizontalAlignment="Center" VerticalAlignment="Center"
                Opacity="0" IsVisible="False"
                CornerRadius="8" Padding="16"
                Background="#80000000">
            <Path Data="M 8 5V 19L 16 12L 8 5 Z"
                  Width="72" Height="72" Stretch="Uniform"
                  Fill="White" />
        </Border>
        
        <!-- Loading Spinner (Overlay) -->
        <ProgressRing x:Name="LoadingSpinner"
                      Width="90" Height="90"
                      HorizontalAlignment="Center" VerticalAlignment="Center"
                      IsVisible="False"
                      Foreground="White" />
        
        <!-- UI Controls (Overlay with Revealer Behavior) -->
        <Border x:Name="UiControls" 
                HorizontalAlignment="Stretch" VerticalAlignment="Stretch"
                Opacity="0" IsVisible="False"
                Background="Transparent">
            
            <!-- Combined Header & Controls Gradient -->
            <Border Background="{StaticResource HeaderAndControlsGradient}">
                
                <!-- Header Bar -->
                <Border x:Name="HeaderBar" 
                        Height="50" VerticalAlignment="Top"
                        Background="{StaticResource HeaderGradient}">
                    <Grid Margin="12,0">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        
                        <!-- Open Menu Button -->
                        <Button x:Name="BtnOpenMenu" Grid.Column="0"
                                Style="{StaticResource CircularMenuButton}"
                                Content="Open"
                                IsVisible="{Binding !IsStartPageVisible}">
                            <Button.Flyout>
                                <MenuFlyout Placement="Bottom">
                                    <MenuItem Header="Open Files" 
                                              Command="{Binding OpenFilesCommand}" />
                                    <MenuItem Header="Open Folder" 
                                              Command="{Binding OpenFolderCommand}" />
                                    <MenuItem Header="Add Files" 
                                              Command="{Binding AddFilesCommand}" />
                                    <Separator />
                                    <MenuItem Header="Add Subtitle Track" 
                                              Command="{Binding AddSubtitleCommand}" />
                                    <MenuItem Header="Add Audio Track" 
                                              Command="{Binding AddAudioCommand}" />
                                </MenuFlyout>
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Window Title (Centered) -->
                        <TextBlock Grid.Column="1" 
                                   Text="Cine"
                                   HorizontalAlignment="Center" VerticalAlignment="Center"
                                   Foreground="White"
                                   FontSize="14" FontWeight="Medium" />
                        
                        <!-- PIP Button -->
                        <ToggleButton x:Name="BtnPip" Grid.Column="2"
                                      Style="{StaticResource CircularToggleButton}"
                                      ToolTip.Tip="Picture-in-Picture">
                            <Path Data="{StaticResource PipIcon}" />
                        </ToggleButton>
                        
                        <!-- Primary Menu Button -->
                        <Button x:Name="BtnPrimaryMenu" Grid.Column="3"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Main Menu">
                            <Path Data="{StaticResource MenuIcon}" />
                            <Button.Flyout>
                                <MenuFlyout Placement="Bottom">
                                    <MenuItem Header="New Window" 
                                              Command="{Binding NewWindowCommand}" />
                                    <MenuItem Header="Preferences" 
                                              Command="{Binding PreferencesCommand}" />
                                    <Separator />
                                    <MenuItem Header="Keyboard Shortcuts" 
                                              Command="{Binding ShortcutsCommand}" />
                                    <MenuItem Header="About Cine" 
                                              Command="{Binding AboutCommand}" />
                                </MenuFlyout>
                            </Button.Flyout>
                        </Button>
                    </Grid>
                </Border>
                
                <!-- Spacer -->
                <Rectangle Height="1" Margin="0" Opacity="0" />
                
                <!-- Controls Box -->
                <Border x:Name="ControlsBox"
                        Height="120" VerticalAlignment="Bottom"
                        Background="{StaticResource ControlsGradient}"
                        Padding="0,0,0,20">
                    
                    <!-- Transport Controls -->
                    <WrapPanel HorizontalAlignment="Center" VerticalAlignment="Top"
                               Margin="13,10,13,0" Spacing="4">
                        
                        <!-- Previous Button -->
                        <Button x:Name="BtnPrevious" 
                                Style="{StaticResource CircularTransportButton}"
                                ToolTip.Tip="Previous (Ctrl+Left)">
                            <Path Data="{StaticResource SkipBackwardIcon}" />
                        </Button>
                        
                        <!-- Play/Pause Button -->
                        <Button x:Name="BtnPlayPause" 
                                Style="{StaticResource CircularTransportButton}"
                                ToolTip.Tip="Play/Pause (Space)">
                            <Path Data="{StaticResource PlayIcon}" 
                                  x:Name="PlayPauseIcon" />
                        </Button>
                        
                        <!-- Next Button -->
                        <Button x:Name="BtnNext" 
                                Style="{StaticResource CircularTransportButton}"
                                ToolTip.Tip="Next (Ctrl+Right)">
                            <Path Data="{StaticResource SkipForwardIcon}" />
                        </Button>
                        
                        <!-- Volume Menu Button -->
                        <Button x:Name="BtnVolumeMenu"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Volume">
                            <Path Data="{StaticResource VolumeMaxIcon}" 
                                  x:Name="VolumeIcon" />
                            <Button.Flyout>
                                <Popup Placement="Top">
                                    <Border Background="{StaticResource PopoverBackground}"
                                            CornerRadius="6" Padding="12"
                                            BorderThickness="1" 
                                            BorderBrush="{StaticResource PopoverBorder}">
                                        <StackPanel Spacing="8">
                                            <ToggleButton x:Name="BtnMuteToggle"
                                                          Style="{StaticResource CircularToggleButton}"
                                                          Content="M" />
                                            <Slider x:Name="VolumeSlider"
                                                    Width="180"
                                                    Minimum="0" Maximum="130"
                                                    Value="{Binding Volume}" />
                                        </StackPanel>
                                    </Border>
                                </Popup>
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Subtitles Menu Button -->
                        <Button x:Name="BtnSubtitlesMenu"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Subtitles">
                            <Path Data="{StaticResource SubtitlesIcon}" />
                            <Button.Flyout>
                                <MenuFlyout ItemsSource="{Binding SubtitleTracks}" />
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Audio Tracks Menu Button -->
                        <Button x:Name="BtnAudioTracksMenu"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Audio Tracks">
                            <Path Data="{StaticResource AudioIcon}" />
                            <Button.Flyout>
                                <MenuFlyout ItemsSource="{Binding AudioTracks}" />
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Video Tracks Menu Button -->
                        <Button x:Name="BtnVideoTracksMenu"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Video Tracks">
                            <Path Data="{StaticResource VideoIcon}" />
                            <Button.Flyout>
                                <MenuFlyout ItemsSource="{Binding VideoTracks}" />
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Separator (Expands) -->
                        <Rectangle Width="1" Opacity="0" />
                        
                        <!-- Playlist Shuffle Toggle -->
                        <ToggleButton x:Name="BtnPlaylistShuffle"
                                      Style="{StaticResource CircularToggleButton}"
                                      ToolTip.Tip="Shuffle Playlist">
                            <Path Data="{StaticResource PlaylistShuffleIcon}" />
                        </ToggleButton>
                        
                        <!-- Playlist Loop Toggle -->
                        <ToggleButton x:Name="BtnPlaylistLoop"
                                      Style="{StaticResource CircularToggleButton}"
                                      ToolTip.Tip="Loop Playlist">
                            <Path Data="{StaticResource PlaylistRepeatIcon}" />
                        </ToggleButton>
                        
                        <!-- File Loop Toggle -->
                        <ToggleButton x:Name="BtnFileLoop"
                                      Style="{StaticResource CircularToggleButton}"
                                      ToolTip.Tip="Loop File">
                            <Path Data="{StaticResource RepeatFileIcon}" />
                        </ToggleButton>
                        
                        <!-- Playlist Button -->
                        <Button x:Name="BtnPlaylist"
                                Style="{StaticResource CircularTransportButton}"
                                ToolTip.Tip="Playlist"
                                Command="{Binding OpenPlaylistCommand}">
                            <Path Data="{StaticResource PlaylistIcon}" />
                        </Button>
                        
                        <!-- Options Menu Button -->
                        <Button x:Name="BtnOptionsMenu"
                                Style="{StaticResource CircularMenuButton}"
                                ToolTip.Tip="Options">
                            <Path Data="{StaticResource OptionsIcon}" />
                            <Button.Flyout>
                                <OptionsFlyout />
                            </Button.Flyout>
                        </Button>
                        
                        <!-- Fullscreen Button -->
                        <Button x:Name="BtnFullscreen"
                                Style="{StaticResource CircularTransportButton}"
                                ToolTip.Tip="Fullscreen (F)"
                                Command="{Binding ToggleFullscreenCommand}">
                            <Path Data="{StaticResource FullscreenIcon}" />
                        </Button>
                        
                    </WrapPanel>
                    
                    <!-- Progress Bar & Time -->
                    <Grid Margin="8,0,20,0" VerticalAlignment="Bottom">
                        <Grid.ColumnDefinitions>
                            <ColumnDefinition Width="Auto" />
                            <ColumnDefinition Width="*" />
                            <ColumnDefinition Width="Auto" />
                        </Grid.ColumnDefinitions>
                        
                        <!-- Time Elapsed -->
                        <TextBlock Grid.Column="0"
                                   Text="{Binding PositionText}"
                                   Style="{StaticResource time-elapsed}" />
                        
                        <!-- Progress Scale -->
                        <Border Grid.Column="1" 
                                Margin="8,0,3,0"
                                VerticalAlignment="Center">
                            <Slider x:Name="ProgressSlider"
                                    Background="Transparent"
                                    Minimum="0" Maximum="1"
                                    Value="{Binding Progress}">
                                <Slider.Styles>
                                    <Style Selector="Slider">
                                        <Setter Property="Template">
                                            <ControlTemplate>
                                                <Grid>
                                                    <!-- Trough -->
                                                    <Border Name="PART_Track"
                                                            Height="4" CornerRadius="2"
                                                            Background="{StaticResource ProgressTroughBackground}" />
                                                    
                                                    <!-- Progress Fill -->
                                                    <Border Name="PART_Fill"
                                                            Height="4" CornerRadius="2"
                                                            Background="White" />
                                                    
                                                    <!-- Thumb -->
                                                    <Border Name="PART_Thumb"
                                                            Width="20" Height="20" CornerRadius="10"
                                                            Background="White"
                                                            BorderThickness="0"
                                                            HorizontalAlignment="Left"
                                                            VerticalAlignment="Center">
                                                        <Border.Shadow>
                                                            <BoxShadow Blur="4" Color="Black" Opacity="0.3" />
                                                        </Border.Shadow>
                                                    </Border>
                                                </Grid>
                                            </ControlTemplate>
                                        </Setter>
                                    </Style>
                                </Slider.Styles>
                            </Slider>
                        </Border>
                        
                        <!-- Time Separator -->
                        <Rectangle Grid.Column="1" 
                                   Width="2" Height="16"
                                   HorizontalAlignment="Center" VerticalAlignment="Center"
                                   Fill="{StaticResource TimeSeparatorBackground}"
                                   Opacity="0.4" />
                        
                        <!-- Time Total -->
                        <TextBlock Grid.Column="2"
                                   Text="{Binding DurationText}"
                                   Style="{StaticResource time-label}" />
                        
                    </Grid>
                </Border>
            </Border>
        </Border>
        
        <!-- Start Page (Overlay) -->
        <Border x:Name="StartPage"
                HorizontalAlignment="Stretch" VerticalAlignment="Stretch"
                Background="{StaticResource StartPageGradient}"
                IsVisible="{Binding IsStartPageVisible}">
            
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center"
                        Spacing="12">
                
                <TextBlock Text="Drag and Drop Files Here"
                           FontSize="24" FontWeight="Medium"
                           Foreground="{StaticResource Gray100}" />
                
                <StackPanel Orientation="Horizontal" Spacing="12"
                            HorizontalAlignment="Center">
                    
                    <Button Content="Open…"
                            Style="{StaticResource PillButtonSuggested}"
                            Command="{Binding OpenFilesCommand}" />
                    
                    <Button Content="Open Folder"
                            Style="{StaticResource PillButton}"
                            Command="{Binding OpenFolderCommand}" />
                    
                </StackPanel>
            </StackPanel>
        </Border>
        
        <!-- Drop Indicator (Overlay) -->
        <Border x:Name="DropIndicator"
                HorizontalAlignment="Stretch" VerticalAlignment="Stretch"
                Margin="12" Padding="24"
                Background="{StaticResource DropIndicatorBackground}"
                BorderBrush="{StaticResource AccentColor}"
                BorderThickness="2" BorderDashArray="4,4"
                CornerRadius="7"
                Opacity="0" IsVisible="False">
            
            <StackPanel HorizontalAlignment="Center" VerticalAlignment="Center"
                        Spacing="12">
                
                <Path Data="{StaticResource DropIcon}"
                      Width="64" Height="64"
                      Fill="{StaticResource AccentColor}" />
                
                <TextBlock x:Name="DropLabel"
                           FontSize="20" FontWeight="Medium"
                           Foreground="{StaticResource AccentColor}" />
                
            </StackPanel>
        </Border>
        
    </Overlay>
</Window>
```

## 3. Component Styles: Button System

### Circular Button Styles
```xml
<!-- ButtonStyles.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- Base Circular Button -->
    <Style Selector="Button.circular">
        <Setter Property="Width" Value="40" />
        <Setter Property="Height" Value="40" />
        <Setter Property="CornerRadius" Value="20" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Padding" Value="0" />
        <Setter Property="HorizontalContentAlignment" Value="Center" />
        <Setter Property="VerticalContentAlignment" Value="Center" />
        
        <!-- OSD Text Shadow -->
        <Setter Property="TextBlock.Foreground" Value="White" />
        <Setter Property="TextBlock.Shadow">
            <Shadow Blur="6" Color="Black" Opacity="0.6" OffsetX="0" OffsetY="1" />
        </Setter>
        
        <!-- Icon Shadow -->
        <Setter Property="Path.Shadow">
            <Shadow Blur="6" Color="Black" Opacity="0.6" OffsetX="0" OffsetY="1" />
        </Setter>
    </Style>
    
    <!-- Circular Flat Button (Transport Controls) -->
    <Style Selector="Button.circular.flat" BasedOn="{StaticResource circular}">
        <Setter Property="Background" Value="Transparent" />
    </Style>
    
    <Style Selector="Button.circular.flat:hover">
        <Setter Property="Background" Value="{StaticResource ButtonHoverBackground}" />
    </Style>
    
    <Style Selector="Button.circular.flat:pressed">
        <Setter Property="Background" Value="{StaticResource ButtonActiveBackground}" />
    </Style>
    
    <Style Selector="Button.circular.flat:disabled">
        <Setter Property="Opacity" Value="0.5" />
        <Setter Property="Path.Shadow">
            <Shadow Blur="5" Color="Black" Opacity="1" OffsetX="0" OffsetY="1" />
        </Setter>
    </Style>
    
    <!-- Circular Toggle Button -->
    <Style Selector="ToggleButton.circular.flat" BasedOn="{StaticResource circular}">
        <Setter Property="Background" Value="Transparent" />
    </Style>
    
    <Style Selector="ToggleButton.circular.flat:hover">
        <Setter Property="Background" Value="{StaticResource ButtonHoverBackground}" />
    </Style>
    
    <Style Selector="ToggleButton.circular.flat:pressed">
        <Setter Property="Background" Value="{StaticResource ButtonActiveBackground}" />
    </Style>
    
    <Style Selector="ToggleButton.circular.flat:checked">
        <Setter Property="Background" Value="{StaticResource ToggleButtonCheckedBackground}" />
        <Setter Property="TextBlock.Foreground" Value="Black" />
        <Setter Property="Path.Fill" Value="Black" />
        <Setter Property="Path.Shadow" Value="{x:Null}" />
        <Setter Property="TextBlock.Shadow" Value="{x:Null}" />
        <Setter Property="BoxShadow">
            <BoxShadow Blur="3" Color="Black" Opacity="0.2" OffsetY="1" />
        </Setter>
    </Style>
    
    <!-- Circular Menu Button -->
    <Style Selector="Button.circular.menu" BasedOn="{StaticResource circular}">
        <Setter Property="MinWidth" Value="80" />
        <Setter Property="Height" Value="32" />
        <Setter Property="CornerRadius" Value="16" />
        <Setter Property="Padding" Value="12,0" />
        <Setter Property="Background" Value="Transparent" />
    </Style>
    
    <!-- Pill Button (Start Page) -->
    <Style Selector="Button.pill">
        <Setter Property="Height" Value="40" />
        <Setter Property="CornerRadius" Value="20" />
        <Setter Property="Padding" Value="24,0" />
        <Setter Property="Background" Value="#1FFFFFFF" /> <!-- rgba(255,255,255,0.12) -->
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="Foreground" Value="{StaticResource Gray100}" />
        <Setter Property="FontSize" Value="14" />
        <Setter Property="FontWeight" Value="Medium" />
    </Style>
    
    <Style Selector="Button.pill:hover">
        <Setter Property="Background" Value="#26FFFFFF" /> <!-- rgba(255,255,255,0.15) -->
    </Style>
    
    <Style Selector="Button.pill:pressed">
        <Setter Property="RenderTransform">
            <ScaleTransform ScaleX="0.98" ScaleY="0.98" />
        </Setter>
    </Style>
    
    <!-- Suggested Action Pill Button -->
    <Style Selector="Button.pill.suggested">
        <Setter Property="Background" Value="{StaticResource Gray100}" />
        <Setter Property="Foreground" Value="Black" />
    </Style>
    
    <Style Selector="Button.pill.suggested:hover">
        <Setter Property="Background" Value="White" />
    </Style>
    
</ResourceDictionary>
```

## 4. Icon System Implementation

### Icon Resource Dictionary
```xml
<!-- Icons.axaml -->
<ResourceDictionary xmlns="https://github.com/avaloniaui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    
    <!-- Playback Icons -->
    <Geometry x:Key="PlayIcon">M 8 5V 19L 16 12L 8 5 Z</Geometry>
    <Geometry x:Key="PauseIcon">M 5 4H 9V 20H 5V 4 Z M 13 4H 17V 20H 13V 4 Z</Geometry>
    <Geometry x:Key="StopIcon">M 4 4H 20V 20H 4V 4 Z</Geometry>
    
    <!-- Skip Icons -->
    <Geometry x:Key="SkipBackwardIcon">M 15.41 7.41L 14 6L 8 12L 14 18L 15.41 16.59L 10.83 12L 15.41 7.41 Z</Geometry>
    <Geometry x:Key="SkipForwardIcon">M 10 6L 8.59 7.41L 13.17 12L 8.59 16.59L 10 18L 16 12L 10 6 Z</Geometry>
    
    <!-- Volume Icons (Multiple levels) -->
    <Geometry x:Key="VolumeMuteIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z M 13 9L 17 5V 19L 13 15Z</Geometry>
    <Geometry x:Key="VolumeLowIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z</Geometry>
    <Geometry x:Key="VolumeMidIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z M 13 9L 17 5V 19L 13 15Z M 18 9L 22 5V 19L 18 15Z</Geometry>
    <Geometry x:Key="VolumeMaxIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z M 13 9L 17 5V 19L 13 15Z M 18 9L 22 5V 19L 18 15Z M 23 9L 27 5V 19L 23 15Z</Geometry>
    <Geometry x:Key="VolumeOverampIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z M 13 9L 17 5V 19L 13 15Z M 18 9L 22 5V 19L 18 15Z M 23 9L 27 5V 19L 23 15Z M 28 9L 32 5V 19L 28 15Z</Geometry>
    
    <!-- Track Icons -->
    <Geometry x:Key="SubtitlesIcon">M 4 4H 20V 20H 4V 4 Z M 8 8H 10V 16H 8V 8 Z M 12 8H 14V 16H 12V 8 Z M 16 8H 18V 16H 16V 8 Z</Geometry>
    <Geometry x:Key="AudioIcon">M 3 9H 5L 9 5H 11V 19H 9V 15H 5V 9 Z</Geometry>
    <Geometry x:Key="VideoIcon">M 4 4H 20V 20H 4V 4 Z M 8 8H 10V 16H 8V 8 Z M 12 8H 14V 16H 12V 8 Z M 16 8H 18V 16H 16V 8 Z</Geometry>
    
    <!-- Playlist Control Icons -->
    <Geometry x:Key="PlaylistShuffleIcon">M 10.59 9.17L 5.41 4L 4 5.41L 9.17 10.58L 10.59 9.17 Z M 14.5 4L 16.54 6.04L 4 18.59L 5.41 20L 17.96 7.46L 20 9.5V 4H 14.5 Z</Geometry>
    <Geometry x:Key="PlaylistRepeatIcon">M 7 7H 17V 10L 21 6L 17 2V 5H 5V 11H 7V 7 Z M 17 17H 7V 14L 3 18L 7 22V 19H 19V 13H 17V 17 Z</Geometry>
    <Geometry x:Key="RepeatFileIcon">M 17 17H 7V 14L 3 18L 7 22V 19H 19V 13H 17V 17 Z</Geometry>
    <Geometry x:Key="PlaylistIcon">M 4 4H 20V 20H 4V 4 Z M 8 8H 10V 16H 8V 8 Z M 12 8H 14V 16H 12V 8 Z M 16 8H 18V 16H 16V 8 Z</Geometry>
    
    <!-- Menu & Options Icons -->
    <Geometry x:Key="OptionsIcon">M 3 18H 21V 16H 3V 18 Z M 3 13H 21V 11H 3V 13 Z M 3 6V 8H 21V 6H 3 Z</Geometry>
    <Geometry x:Key="MenuIcon">M 3 18H 21V 16H 3V 18 Z M 3 13H 21V 11H 3V 13 Z M 3 6V 8H 21V 6H 3 Z</Geometry>
    
    <!-- View Icons -->
    <Geometry x:Key="FullscreenIcon">M 7 14H 5V 19H 10V 17H 7V 14 Z M 5 10H 7V 7H 10V 5H 5V 10 Z M 17 17H 14V 19H 19V 14H 17V 17 Z M 14 5V 7H 17V 10H 19V 5H 14 Z</Geometry>
    <Geometry x:Key="RestoreIcon">M 7 14H 5V 19H 10V 17H 7V 14 Z M 5 10H 7V 7H 10V 5H 5V 10 Z M 17 17H 14V 19H 19V 14H 17V 17 Z M 14 5V 7H 17V 10H 19V 5H 14 Z</Geometry>
    <Geometry x:Key="PipIcon">M 19 11H 13V 5H 19V 11 Z M 19 19H 13V 13H 19V 19 Z M 11 11H 5V 5H 11V 11 Z M 11 19H 5V 13H 11V 19 Z</Geometry>
    
    <!-- Drop & Status Icons -->
    <Geometry x:Key="DropIcon">M 19 13C 19.7 13 20.37 13.13 21 13.35V 8L 14 2H 6C 4.9 2 4.01 2.9 4.01 4L 4 20C 4 21.1 4.89 22 5.99 22H 13.54C 12.58 20.94 12 19.54 12 18C 12 15.24 14.24 13 17 13C 17.65 13 18.27 13.1 18.86 13.28L 19 13 Z M 14 3.5L 18.5 8H 14V 3.5 Z M 23 18C 23 20.21 21.21 22 19 22S 15 20.21 15 18C 15 15.79 16.79 14 19 14S 23 15.79 23 18 Z M 20.5 18.5L 18 21L 15.5 18.5L 16.21 17.79L 18 19.59L 19.79 17.79L 20.5 18.5 Z</Geometry>
    
    <!-- Icon Style for Consistent Sizing -->
    <Style Selector="Path.icon">
        <Setter Property="Width" Value="24" />
        <Setter Property="Height" Value="24" />
        <Setter Property="Stretch" Value="Uniform" />
        <Setter Property="Fill" Value="{DynamicResource SystemControlForegroundBaseHighBrush}" />
    </Style>
    
</ResourceDictionary>
```

## 5. Animation & Transition System

### Revealer Animation Behaviors
```csharp
// RevealerBehavior.cs
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;

namespace Cine.Avalonia.Behaviors
{
    public class RevealerBehavior : AvaloniaObject
    {
        public static readonly AttachedProperty<bool> IsRevealedProperty =
            AvaloniaProperty.RegisterAttached<RevealerBehavior, Control, bool>(
                "IsRevealed", defaultValue: false);
        
        public static readonly AttachedProperty<int> TransitionDurationProperty =
            AvaloniaProperty.RegisterAttached<RevealerBehavior, Control, int>(
                "TransitionDuration", defaultValue: 300);
        
        public static readonly AttachedProperty<RevealerTransitionType> TransitionTypeProperty =
            AvaloniaProperty.RegisterAttached<RevealerBehavior, Control, RevealerTransitionType>(
                "TransitionType", defaultValue: RevealerTransitionType.SlideUp);
        
        static RevealerBehavior()
        {
            IsRevealedProperty.Changed.AddClassHandler<Control>(OnIsRevealedChanged);
        }
        
        public static bool GetIsRevealed(Control element) => element.GetValue(IsRevealedProperty);
        public static void SetIsRevealed(Control element, bool value) => element.SetValue(IsRevealedProperty, value);
        
        public static int GetTransitionDuration(Control element) => element.GetValue(TransitionDurationProperty);
        public static void SetTransitionDuration(Control element, int value) => element.SetValue(TransitionDurationProperty, value);
        
        public static RevealerTransitionType GetTransitionType(Control element) => element.GetValue(TransitionTypeProperty);
        public static void SetTransitionType(Control element, RevealerTransitionType value) => element.SetValue(TransitionTypeProperty, value);
        
        private static async void OnIsRevealedChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            var isRevealed = (bool)args.NewValue!;
            var duration = GetTransitionDuration(control);
            var transitionType = GetTransitionType(control);
            
            // Set initial state
            if (!isRevealed)
            {
                control.Opacity = 0;
                control.IsVisible = false;
                return;
            }
            
            // Make visible before animation
            control.IsVisible = true;
            
            // Create animation based on transition type
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(duration),
                Easing = new CubicEaseOut()
            };
            
            switch (transitionType)
            {
                case RevealerTransitionType.SlideUp:
                    var translateY = control.RenderTransform as TranslateTransform ?? new TranslateTransform();
                    control.RenderTransform = translateY;
                    
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.Zero,
                            Setters =
                            {
                                new Setter(Visual.OpacityProperty, 1.0),
                                new Setter(TranslateTransform.YProperty, 0.0)
                            }
                        });
                    break;
                    
                case RevealerTransitionType.Fade:
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.Zero,
                            Setters = { new Setter(Visual.OpacityProperty, 0.0) }
                        });
                    
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.FromMilliseconds(duration),
                            Setters = { new Setter(Visual.OpacityProperty, 1.0) }
                        });
                    break;
                    
                case RevealerTransitionType.SlideDown:
                    var translateY2 = control.RenderTransform as TranslateTransform ?? new TranslateTransform();
                    control.RenderTransform = translateY2;
                    
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.Zero,
                            Setters = 
                            {
                                new Setter(Visual.OpacityProperty, 0.0),
                                new Setter(TranslateTransform.YProperty, -20.0)
                            }
                        });
                    
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.FromMilliseconds(duration),
                            Setters =
                            {
                                new Setter(Visual.OpacityProperty, 1.0),
                                new Setter(TranslateTransform.YProperty, 0.0)
                            }
                        });
                    break;
            }
            
            // Run animation
            await animation.RunAsync(control);
        }
    }
    
    public enum RevealerTransitionType
    {
        SlideUp,
        SlideDown,
        Fade,
        Crossfade
    }
}
```

### UI Auto-hide Behavior
```csharp
// UiAutoHideBehavior.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cine.Avalonia.Behaviors
{
    public class UiAutoHideBehavior : AvaloniaObject
    {
        private static CancellationTokenSource? _hideCts;
        private static DateTime _lastInteractionTime = DateTime.Now;
        
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<UiAutoHideBehavior, Control, bool>(
                "IsEnabled", defaultValue: false);
        
        public static readonly AttachedProperty<int> HideDelayProperty =
            AvaloniaProperty.RegisterAttached<UiAutoHideBehavior, Control, int>(
                "HideDelay", defaultValue: 2000); // 2 seconds
        
        public static readonly AttachedProperty<Control?> UiControlsProperty =
            AvaloniaProperty.RegisterAttached<UiAutoHideBehavior, Control, Control?>(
                "UiControls", defaultValue: null);
        
        static UiAutoHideBehavior()
        {
            IsEnabledProperty.Changed.AddClassHandler<Control>(OnIsEnabledChanged);
        }
        
        public static bool GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(Control element, bool value) => element.SetValue(IsEnabledProperty, value);
        
        public static int GetHideDelay(Control element) => element.GetValue(HideDelayProperty);
        public static void SetHideDelay(Control element, int value) => element.SetValue(HideDelayProperty, value);
        
        public static Control? GetUiControls(Control element) => element.GetValue(UiControlsProperty);
        public static void SetUiControls(Control element, Control? value) => element.SetValue(UiControlsProperty, value);
        
        private static void OnIsEnabledChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            var isEnabled = (bool)args.NewValue!;
            
            if (isEnabled)
            {
                // Attach event handlers
                control.PointerMoved += OnPointerMoved;
                control.PointerPressed += OnPointerPressed;
                control.KeyDown += OnKeyDown;
                
                // Start hide timer
                StartHideTimer(control);
            }
            else
            {
                // Remove event handlers
                control.PointerMoved -= OnPointerMoved;
                control.PointerPressed -= OnPointerPressed;
                control.KeyDown -= OnKeyDown;
                
                // Cancel hide timer
                CancelHideTimer();
            }
        }
        
        private static void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            _lastInteractionTime = DateTime.Now;
            ShowUiControls(sender as Control);
            RestartHideTimer(sender as Control);
        }
        
        private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _lastInteractionTime = DateTime.Now;
            ShowUiControls(sender as Control);
            RestartHideTimer(sender as Control);
        }
        
        private static void OnKeyDown(object? sender, KeyEventArgs e)
        {
            _lastInteractionTime = DateTime.Now;
            ShowUiControls(sender as Control);
            RestartHideTimer(sender as Control);
        }
        
        private static void ShowUiControls(Control? control)
        {
            if (control == null) return;
            
            var uiControls = GetUiControls(control);
            if (uiControls != null)
            {
                RevealerBehavior.SetIsRevealed(uiControls, true);
            }
        }
        
        private static void HideUiControls(Control? control)
        {
            if (control == null) return;
            
            var uiControls = GetUiControls(control);
            if (uiControls != null)
            {
                RevealerBehavior.SetIsRevealed(uiControls, false);
            }
        }
        
        private static void StartHideTimer(Control control)
        {
            CancelHideTimer();
            
            _hideCts = new CancellationTokenSource();
            var token = _hideCts.Token;
            
            Task.Run(async () =>
            {
                await Task.Delay(GetHideDelay(control), token);
                
                if (!token.IsCancellationRequested)
                {
                    // Check if enough time has passed since last interaction
                    var timeSinceInteraction = DateTime.Now - _lastInteractionTime;
                    if (timeSinceInteraction.TotalMilliseconds >= GetHideDelay(control))
                    {
                        await control.Dispatcher.InvokeAsync(() =>
                        {
                            HideUiControls(control);
                        });
                    }
                }
            }, token);
        }
        
        private static void RestartHideTimer(Control? control)
        {
            if (control != null)
            {
                StartHideTimer(control);
            }
        }
        
        private static void CancelHideTimer()
        {
            _hideCts?.Cancel();
            _hideCts?.Dispose();
            _hideCts = null;
        }
    }
}
```

## 6. Responsive Design Implementation

### Breakpoint System
```csharp
// BreakpointBehavior.cs
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;

namespace Cine.Avalonia.Behaviors
{
    public class BreakpointBehavior : AvaloniaObject
    {
        public static readonly AttachedProperty<double> MaxWidthProperty =
            AvaloniaProperty.RegisterAttached<BreakpointBehavior, Control, double>(
                "MaxWidth", defaultValue: 495);
        
        public static readonly AttachedProperty<Control?> TargetControlProperty =
            AvaloniaProperty.RegisterAttached<BreakpointBehavior, Control, Control?>(
                "TargetControl", defaultValue: null);
        
        public static readonly AttachedProperty<AvaloniaProperty?> TargetPropertyProperty =
            AvaloniaProperty.RegisterAttached<BreakpointBehavior, Control, AvaloniaProperty?>(
                "TargetProperty", defaultValue: null);
        
        public static readonly AttachedProperty<object?> TargetValueProperty =
            AvaloniaProperty.RegisterAttached<BreakpointBehavior, Control, object?>(
                "TargetValue", defaultValue: null);
        
        static BreakpointBehavior()
        {
            MaxWidthProperty.Changed.AddClassHandler<Control>(OnMaxWidthChanged);
        }
        
        public static double GetMaxWidth(Control element) => element.GetValue(MaxWidthProperty);
        public static void SetMaxWidth(Control element, double value) => element.SetValue(MaxWidthProperty, value);
        
        public static Control? GetTargetControl(Control element) => element.GetValue(TargetControlProperty);
        public static void SetTargetControl(Control element, Control? value) => element.SetValue(TargetControlProperty, value);
        
        public static AvaloniaProperty? GetTargetProperty(Control element) => element.GetValue(TargetPropertyProperty);
        public static void SetTargetProperty(Control element, AvaloniaProperty? value) => element.SetValue(TargetPropertyProperty, value);
        
        public static object? GetTargetValue(Control element) => element.GetValue(TargetValueProperty);
        public static void SetTargetValue(Control element, object? value) => element.SetValue(TargetValueProperty, value);
        
        private static void OnMaxWidthChanged(Control control, AvaloniaPropertyChangedEventArgs args)
        {
            // Listen to window size changes
            if (control.GetVisualRoot() is Window window)
            {
                window.PropertyChanged += (sender, e) =>
                {
                    if (e.Property == Window.ClientSizeProperty)
                    {
                        UpdateBreakpoint(control, window);
                    }
                };
                
                // Initial update
                UpdateBreakpoint(control, window);
            }
        }
        
        private static void UpdateBreakpoint(Control control, Window window)
        {
            var maxWidth = GetMaxWidth(control);
            var targetControl = GetTargetControl(control);
            var targetProperty = GetTargetProperty(control);
            var targetValue = GetTargetValue(control);
            
            if (targetControl == null || targetProperty == null || targetValue == null)
                return;
            
            // Convert DIP to pixels (assuming 96 DPI)
            var currentWidth = window.ClientSize.Width;
            
            if (currentWidth <= maxWidth)
            {
                // Apply breakpoint condition
                targetControl.SetValue(targetProperty, targetValue);
            }
            else
            {
                // Revert to default (clear the value)
                targetControl.ClearValue(targetProperty);
            }
        }
    }
}
```

### Responsive Layout Adjustments
```xml
<!-- In MainWindow.axaml -->
<Window ...>
    
    <!-- Breakpoint for controls separator -->
    <Window.Styles>
        <Style Selector="Border#ControlsSeparatorBreakpoint">
            <Setter Property="behaviors:BreakpointBehavior.MaxWidth" Value="495" />
            <Setter Property="behaviors:BreakpointBehavior.TargetControl" Value="{Binding ElementName=ControlsSeparator}" />
            <Setter Property="behaviors:BreakpointBehavior.TargetProperty" Value="{x:Static Border.IsVisibleProperty}" />
            <Setter Property="behaviors:BreakpointBehavior.TargetValue" Value="False" />
        </Style>
    </Window.Styles>
    
    <!-- In UI controls section -->
    <Rectangle x:Name="ControlsSeparator"
               Grid.Column="1" 
               Width="1" Height="16"
               HorizontalAlignment="Center" VerticalAlignment="Center"
               Fill="{StaticResource TimeSeparatorBackground}"
               Opacity="0.4"
               classes="ControlsSeparatorBreakpoint" />
    
</Window>
```

## 7. Implementation Priority & Phasing

### Phase 1: Foundation (Week 1)
1. **Color System**: Implement centralized color resources
2. **Typography**: Set up font system and text styles
3. **Basic Layout**: Convert to overlay-based structure
4. **Button Styles**: Implement circular button system

### Phase 2: Core Components (Week 2)
1. **Icon System**: Implement symbolic icon resources
2. **Progress Bar**: Custom slider with Python styling
3. **Menu System**: File and primary menu buttons
4. **Volume Control**: Popover with mute toggle

### Phase 3: Advanced Features (Week 3)
1. **Animation System**: Revealer behaviors and transitions
2. **Auto-hide**: UI visibility with mouse/keyboard tracking
3. **Responsive Design**: Breakpoint system
4. **Start Page**: Drag-and-drop interface

### Phase 4: Polish & Integration (Week 4)
1. **Visual Effects**: Gradients, shadows, OSD styling
2. **Accessibility**: Keyboard navigation, screen reader support
3. **Performance**: Optimize animations and rendering
4. **Testing**: Cross-platform validation

## 8. Testing & Validation Checklist

### Visual Consistency Tests
- [ ] Color matching against Python screenshots
- [ ] Typography alignment (font, size, weight)
- [ ] Button sizing and spacing
- [ ] Icon sizing and positioning
- [ ] Gradient rendering quality
- [ ] Shadow effects and opacity

### Functional Tests
- [ ] UI auto-hide/show behavior
- [ ] Revealer animations (duration, easing)
- [ ] Menu popovers and flyouts
- [ ] Volume control interaction
- [ ] Progress bar dragging
- [ ] Responsive breakpoints

### Performance Tests
- [ ] Animation smoothness (60fps target)
- [ ] Memory usage with multiple overlays
- [ ] GPU acceleration for gradients
- [ ] Startup time with resource loading

### Accessibility Tests
- [ ] Keyboard navigation (Tab, Arrow keys)
- [ ] Screen reader compatibility
- [ ] High contrast mode support
- [ ] Focus indicators visibility

## 9. Resources & Assets

### Required Asset Files
1. **Icon SVGs**: Convert Python symbolic icons to SVG paths
2. **Color Swatches**: Extract exact colors from Python CSS
3. **Gradient Definitions**: Recreate linear gradients
4. **Font Files**: Ensure Consolas/Courier New availability

### Reference Materials
1. **Python Screenshots**: `window.png`, `video.png`, `options.png`, `preferences.png`
2. **GTK4 Documentation**: Adwaita component specifications
3. **Avalonia Documentation**: Custom control templates
4. **Design Specifications**: Pixel measurements from reference

## 10. Success Metrics

### Quantitative Metrics
- **Visual Accuracy**: 95%+ pixel matching with reference
- **Performance**: <16ms frame time for animations
- **Memory**: <50MB additional overhead
- **Load Time**: <100ms for resource initialization

### Qualitative Metrics
- **User Experience**: Intuitive interaction patterns
- **Visual Polish**: Professional, polished appearance
- **Platform Consistency**: Feels native on Windows
- **Accessibility**: Fully accessible to all users

## Conclusion

This comprehensive solution set provides everything needed to achieve pixel-perfect alignment between the Avalonia implementation and the Python reference. By following this phased approach, the team can systematically address each mismatch while maintaining code quality and performance.

The key to success is starting with the foundation (colors, typography, layout) and progressively building up to the more complex features (animations, responsive design). Regular testing against the Python screenshots will ensure visual accuracy throughout the implementation process. 
                            {
                                new Setter(Visual.OpacityProperty, 0.0),
                                new Setter(TranslateTransform.YProperty, 20.0)
                            }
                        });
                    
                    animation.Children.Add(
                        new KeyFrame
                        {
                            KeyTime = TimeSpan.FromMilliseconds(duration),
                            Setters =