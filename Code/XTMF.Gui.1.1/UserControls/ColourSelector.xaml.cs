/*
    Copyright 2014 Travel Modelling Group, Department of Civil Engineering, University of Toronto

    This file is part of XTMF.

    XTMF is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    XTMF is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with XTMF.  If not, see <http://www.gnu.org/licenses/>.
*/
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace XTMF.Gui
{
    /// <summary>
    /// Interaction logic for ColourSelector.xaml
    /// </summary>
    public partial class ColourSelector : UserControl
    {
        public ColourSelector()
        {
            InitializeComponent();
        }

        public event Action<Color> ColourSelected;

        private void redSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            var cs = ColourSelected;
            if ( cs != null )
            {
                cs( Color.FromRgb( (byte)this.redSlider.Value, (byte)this.greenSlider.Value, (byte)this.blueSlider.Value ) );
            }
            ResetStops();
        }

        private void ResetStops()
        {
            var r = (byte)this.redSlider.Value;
            var g = (byte)this.greenSlider.Value;
            var b = (byte)this.blueSlider.Value;
            RedStart.Color = Color.FromRgb( 0, g, b );
            GreenStart.Color = Color.FromRgb( r, 0, b );
            BlueStart.Color = Color.FromRgb( r, g, 0 );

            RedStop.Color = Color.FromRgb( 255, g, b );
            GreenStop.Color = Color.FromRgb( r, 255, b );
            BlueStop.Color = Color.FromRgb( r, g, 255 );
        }
    }
}