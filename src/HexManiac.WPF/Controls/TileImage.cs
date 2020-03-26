﻿using HavenSoft.HexManiac.Core.ViewModels.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace HavenSoft.HexManiac.WPF.Controls {
   public class TileImage : Image {

      private TileViewModel ViewModel => (TileViewModel)DataContext;

      public TileImage() {
         DataContextChanged += (sender, e) => UpdateDataContext(e);
         Stretch = Stretch.None;
      }

      private void UpdateDataContext(DependencyPropertyChangedEventArgs e) {
         var oldValue = e.OldValue as INotifyPropertyChanged;
         if (oldValue != null) oldValue.PropertyChanged -= HandleDataContextPropertyChanged;
         var newValue = e.NewValue as INotifyPropertyChanged;
         if (newValue != null) newValue.PropertyChanged += HandleDataContextPropertyChanged;
         UpdateSource();
      }

      private void HandleDataContextPropertyChanged(object sender, PropertyChangedEventArgs e) {
         UpdateSource();
      }

      public void UpdateSource() {
         var bitsPerPixel = 4; // TODO
         var pixels = new byte[8 * bitsPerPixel];
         Array.Copy(ViewModel.DataStore, ViewModel.Start, pixels, 0, pixels.Length);
         var palette = CreatePalette();
         int stride = 4; // Width(8) * bytesPerPixel(.5)
         var format = PixelFormats.Indexed4; // 16 possible colors
         var source = BitmapSource.Create(8, 8, 96, 96, format, palette, pixels, stride);
         Source = source;
      }

      public BitmapPalette CreatePalette() {
         var colors = ViewModel.Palette.Select(Convert16BitColor).ToList();
         return new BitmapPalette(colors);
      }

      public static Color Convert16BitColor(short color) {
         byte b = (byte)((color >> 10) & 0b11111);
         byte g = (byte)((color >> 5) & 0b11111);
         byte r = (byte)((color >> 0) & 0b11111);

         return Color.FromArgb(255, (byte)(r << 3), (byte)(g << 3), (byte)(b << 3));
      }
   }

   public class PaletteColorConverter : IValueConverter {
      public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
         var color = (short)value;
         var wpfColor = TileImage.Convert16BitColor(color);
         return new SolidColorBrush(wpfColor);
      }

      public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
         throw new NotImplementedException();
      }
   }
}