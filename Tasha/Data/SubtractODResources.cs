﻿/*
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
using Datastructure;
using TMG.Input;
using XTMF;
namespace Tasha.Data
{

    public class SubtractODResources : IReadODData<float>
    {

        public string Name { get; set; }

        public float Progress { get; set; }

        public Tuple<byte, byte, byte> ProgressColour { get { return new Tuple<byte, byte, byte>(50, 150, 50); } }

        [SubModelInformation(Required = true, Description = "The first matrix (First - Second)")]
        public IResource First;

        [SubModelInformation(Required = true, Description = "The second matrix (First - Second)")]
        public IResource Second;

        public IEnumerable<ODData<float>> Read()
        {
            var firstSparse = First.AcquireResource<SparseTwinIndex<float>>();
            var first = firstSparse.GetFlatData();
            var second = Second.AcquireResource<SparseTwinIndex<float>>().GetFlatData();
            ODData<float> point = new ODData<float>();
            for(int i = 0; i < first.Length; i++)
            {
                point.O = firstSparse.GetSparseIndex(i);
                for(int j = 0; j < first[i].Length; j++)
                {
                    point.D = firstSparse.GetSparseIndex(i, j);
                    point.Data = first[i][j] - second[i][j];
                    yield return point;
                }
            }
        }

        public bool RuntimeValidation(ref string error)
        {
            if(!First.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the First resource is not of type SparseTwinIndex<float>!";
                return false;
            }
            if(!Second.CheckResourceType<SparseTwinIndex<float>>())
            {
                error = "In '" + Name + "' the Second resource is not of type SparseTwinIndex<float>!";
                return false;
            }
            return true;
        }
    }

}
