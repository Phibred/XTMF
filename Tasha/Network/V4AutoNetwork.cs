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
using TMG;
using TMG.Input;
using XTMF;
using Datastructure;
namespace Tasha.Network
{
    [ModuleInformation(Description =
        @"This module is used for providing multiple time definitions for network data.  Implemented first for GTAModel V4.0.")]
    public sealed class V4AutoNetwork : INetworkData
    {
        [RunParameter("No Unload", false, "Don't unload the data between iterations.")]
        public bool NoUnload;

        [RootModule]
        public ITravelDemandModel Root;

        [DoNotAutomate]
        private IIterativeModel IterativeRoot;

        private SparseArray<IZone> ZoneArray;

        [ModuleInformation(Description =
            @"This module defines a time period and store the network data for that time period.  Implemented first for GTAModel V4.0.")]
        public sealed class TimePeriodNetworkData : IModule
        {
            [RunParameter("Start Time", "6:00AM", typeof(Time), "The start time for the period.")]
            public Time StartTime;
            [RunParameter("End Time", "9:00AM", typeof(Time), "The end time for the period, (exclusive).")]
            public Time EndTime;

            private int NumberOfZones;

            private float[] Data;

            [SubModelInformation(Required = true, Description = "Provides Travel Time data.")]
            public IReadODData<float> TravelTimeReader;

            [SubModelInformation(Required = true, Description = "Provides cost data.")]
            public IReadODData<float> CostReader;

            /// <summary>
            /// This value is used to do averaged travel times
            /// </summary>
            int TimesLoaded = 0;

            internal void LoadData(SparseArray<IZone> zoneArray)
            {
                var zones = zoneArray.GetFlatData();
                this.NumberOfZones = zones.Length;
                var dataSize = zones.Length * zones.Length * (int)DataTypes.NumberOfDataTypes;
                // now that we have zones we can build our data
                var data = Data == null || dataSize != Data.Length ? new float[dataSize] : Data;
                //now we need to load in each type
                LoadData(data, this.TravelTimeReader, (int)DataTypes.TravelTime, zoneArray, TimesLoaded);
                LoadData(data, this.CostReader, (int)DataTypes.Cost, zoneArray, TimesLoaded);
                TimesLoaded++;
                // now store it
                this.Data = data;
            }

            private void LoadData(float[] data, IReadODData<float> readODData, int dataTypeOffset, SparseArray<IZone> zoneArray, int timesLoaded)
            {
                if(readODData == null)
                {
                    return;
                }
                var zones = zoneArray.GetFlatData();
                var numberOfZones = zones.Length;
                var dataTypes = (int)DataTypes.NumberOfDataTypes;
                int previousPointO = -1;
                int previousFlatO = -1;
                if(timesLoaded == 0)
                {
                    foreach(var point in readODData.Read())
                    {
                        var o = point.O == previousPointO ? previousFlatO : zoneArray.GetFlatIndex(point.O);
                        var d = zoneArray.GetFlatIndex(point.D);
                        if(o >= 0 & d >= 0)
                        {
                            previousPointO = point.O;
                            previousFlatO = o;
                            var index = (o * numberOfZones + d) * dataTypes + dataTypeOffset;
                            data[index] = point.Data;
                        }
                    }
                }
                else
                {
                    foreach(var point in readODData.Read())
                    {
                        var o = point.O == previousPointO ? previousFlatO : zoneArray.GetFlatIndex(point.O);
                        var d = zoneArray.GetFlatIndex(point.D);
                        if(o >= 0 & d >= 0)
                        {
                            previousPointO = point.O;
                            previousFlatO = o;
                            var index = (o * numberOfZones + d) * dataTypes + dataTypeOffset;
                            data[index] = data[index] * 0.75f + point.Data * 0.25f;
                        }
                    }
                }
            }

            internal void UnloadData()
            {

            }

            internal bool GetDataIfInTimePeriod(Time time, int flatO, int flatD, out float travelTime, out float travelCost)
            {
                if(time < this.StartTime | time >= this.EndTime)
                {
                    travelTime = 0;
                    travelCost = 0;
                    return false;
                }
                var index = (this.NumberOfZones * flatO + flatD) * ((int)DataTypes.NumberOfDataTypes);
                travelTime = this.Data[index + (int)DataTypes.TravelTime];
                travelCost = this.Data[index + (int)DataTypes.Cost];
                return true;
            }

            public string Name { get; set; }

            public float Progress
            {
                get { return 0f; }
            }

            public Tuple<byte, byte, byte> ProgressColour
            {
                get { return null; }
            }

            public bool RuntimeValidation(ref string error)
            {
                return true;
            }
        }

        [SubModelInformation(Required = false, Description = "The data for each time period for this network")]
        public TimePeriodNetworkData[] TimePeriods;

        private enum DataTypes
        {
            TravelTime = 0,
            Cost = 1,
            NumberOfDataTypes = 2
        }

        public string Name
        {
            get;
            set;
        }

        [RunParameter("Network Name", "Auto", "The name of this network data.")]
        public string NetworkType
        {
            get;
            set;
        }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return new Tuple<byte, byte, byte>(100, 200, 100); }
        }

        public INetworkData GiveData()
        {
            return this;
        }

        public bool Loaded
        {
            get;
            private set;
        }

        public void LoadData()
        {
            // setup our zones
            var zoneArray = this.Root.ZoneSystem.ZoneArray;
            this.ZoneArray = zoneArray;
            if(!this.Loaded)
            {
                for(int i = 0; i < this.TimePeriods.Length; i++)
                {
                    this.TimePeriods[i].LoadData(zoneArray);
                }
                this.Loaded = true;
            }
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            this.Loaded = false;
            this.IterativeRoot = this.Root as IIterativeModel;
            return true;
        }

        public float TravelCost(IZone start, IZone end, Time time)
        {
            return TravelCost(this.ZoneArray.GetFlatIndex(start.ZoneNumber), this.ZoneArray.GetFlatIndex(end.ZoneNumber), time);
        }

        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return TravelTime(this.ZoneArray.GetFlatIndex(origin.ZoneNumber), this.ZoneArray.GetFlatIndex(destination.ZoneNumber), time);
        }

        public bool GetAllData(IZone start, IZone end, Time time, out Time ivtt, out float cost)
        {
            if(GetData(this.ZoneArray.GetFlatIndex(start.ZoneNumber), this.ZoneArray.GetFlatIndex(end.ZoneNumber), time, out float localIvtt, out cost))
            {
                ivtt = Time.FromMinutes(localIvtt);
                return true;
            }
            ivtt = Time.Zero;
            return false;
        }

        private bool GetData(int flatO, int flatD, Time time, out float travelTime, out float cost)
        {
            for(int i = 0; i < this.TimePeriods.Length; i++)
            {
                if(this.TimePeriods[i].GetDataIfInTimePeriod(time, flatO, flatD, out travelTime, out cost))
                {
                    return true;
                }
            }
            travelTime = cost = 0f;
            return false;
        }

        public Time TravelTime(int flatOrigin, int flatDestination, Time time)
        {
            float travelTime, cost;
            GetData(flatOrigin, flatDestination, time, out travelTime, out cost);
            return Time.FromMinutes(travelTime);
        }

        public float TravelCost(int flatOrigin, int flatDestination, Time time)
        {
            float travelTime, cost;
            GetData(flatOrigin, flatDestination, time, out travelTime, out cost);
            return cost;
        }

        public bool GetAllData(int start, int end, Time time, out float ivtt, out float cost)
        {
            for(int i = 0; i < this.TimePeriods.Length; i++)
            {
                if(this.TimePeriods[i].GetDataIfInTimePeriod(time, start, end, out ivtt, out cost))
                {
                    return true;
                }
            }
            ivtt = cost = 0f;
            return false;
        }

        public void UnloadData()
        {
            if(!NoUnload)
            {
                this.ZoneArray = null;
                this.Loaded = false;
                for(int i = 0; i < this.TimePeriods.Length; i++)
                {
                    this.TimePeriods[i].UnloadData();
                }
            }
        }

        public bool ValidOD(IZone start, IZone end, Time time)
        {
            return true;
        }

        public bool ValidOD(int flatOrigin, int flatDestination, Time time)
        {
            return true;
        }
    }
}
