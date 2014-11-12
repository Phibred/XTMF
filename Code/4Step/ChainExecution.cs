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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using XTMF;

namespace James.UTDM
{
    public class ChainExecution : IModelSystemTemplate
    {
        [RunParameter("Input Directory", "../../Input", "The input directory for the Model System")]
        public string InputBaseDirectory
        {
            get;
            set;
        }

        [RunParameter("Output Directory", ".", "The output directory for the Model System")]
        public string OutputBaseDirectory
        {
            get;
            set;
        }

        [SubModelInformation(Required=true, Description="The modules to execute")]
        public List<ISelfContainedModule> ToExecute;

        public bool ExitRequest()
        {
            return false;
        }

        public void Start()
        {
            if (ToExecute == null)
            {
                return;
            }

            foreach (var module in this.ToExecute)
            {
                module.Start();
            }
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            set;
        }


        private static Tuple<byte, byte, byte> _ProgressColour = new Tuple<byte, byte, byte>(50, 150, 50);
        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return _ProgressColour; }
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }
    }
}
