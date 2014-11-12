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
using System.Windows;
using System.Windows.Controls;

namespace XTMF.Gui.Pages
{
    /// <summary>
    /// Interaction logic for DeleteProjectPage.xaml
    /// </summary>
    public partial class DeleteProjectPage : UserControl, IXTMFPage
    {
        private static XTMFPage[] _Path = new XTMFPage[] { XTMFPage.StartPage, XTMFPage.ProjectSelectPage, XTMFPage.ProjectSettingsPage, XTMFPage.DeleteProjectPage };
        private IProject CurrentProject;

        private SingleWindowGUI XTMF;

        public DeleteProjectPage(SingleWindowGUI xmtfWindow)
        {
            this.XTMF = xmtfWindow;
            InitializeComponent();
            this.Loaded += new RoutedEventHandler( DeleteProjectPage_Loaded );
        }

        public XTMFPage[] Path
        {
            get { return _Path; }
        }

        public void SetActive(object data)
        {
        }

        private void CancelButton_Clicked(object obj)
        {
            this.CurrentProject = null;
            this.XTMF.Navigate( XTMFPage.ProjectSettingsPage );
        }

        private void DeleteButton_Clicked(object obj)
        {
            this.XTMF.Delete( this.CurrentProject );
            this.CurrentProject = null;
            this.XTMF.Navigate( XTMFPage.ProjectSelectPage );
        }

        private void DeleteProjectPage_Loaded(object sender, RoutedEventArgs e)
        {
            this.CurrentProject = this.XTMF.CurrentProject;
            if ( this.CurrentProject != null )
            {
                ProjectNameLabel.Content = this.CurrentProject.Name;
            }
        }
    }
}