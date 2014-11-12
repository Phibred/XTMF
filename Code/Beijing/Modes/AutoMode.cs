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
using Tasha.Common;
using TMG;
using XTMF;

namespace Beijing.Modes
{
    public sealed class AutoMode : ITashaMode
    {
        [RootModule]
        public ITashaRuntime Root;

        [DoNotAutomate]
        private INetworkData AutoData;

        [RunParameter( "Mode Name", "Auto", "The name of the mode being modelled." )]
        public string ModeName { get; set; }


        [RunParameter( "Income Name", "Income", "The name of the households income attribute." )]
        public string IncomeName;

        [RunParameter( "TravelTimeFactor", -1.0f, "The scaling component based on distance" )]
        public float TravelTimeFactor;

        [RunParameter( "LinearTravelTimeWeight", -1.0f, "The estimated scaling component based on distance" )]
        public float LinearTravelTimeWeight;

        [RunParameter( "NonLinearTravelTimeWeight", -1.0f, "The estimated scaling component based on distance" )]
        public float NonLinearTravelTimeFactor;

        [RunParameter( "TravelTimeConstant", 0f, "The constant component based on distance" )]
        public float ModeConstant;

        [RunParameter( "TravelCostWeight", 8.0f, "The weight of travel time on the utility" )]
        public float TravelCostWeight;

        [RunParameter( "TravelCostFactor", -1.0f, "The scaling component based on distance" )]
        public float TravelCostFactor;

        [RunParameter( "Youth", 0f, "A constant if the person is a youth" )]
        public float Youth;

        [RunParameter( "YoungAdult", 0f, "A constant if the person is a youth" )]
        public float YoungAdult;

        [RunParameter( "Female", 0f, "The constant for being a female" )]
        public float Female;

        [RunParameter( "Minimum Age", 0, "The minimum age to use this mode" )]
        public int MinAge;

        [RunParameter( "DriversLicense", 0.0f, "A constant applied if a person has a driver's license." )]
        public float DriversLicense;

        [RunParameter( "ManyCarsOwned", 0.0f, "A constant applied if a household has > 1 car." )]
        public float ManyCarsOwned;

        [RunParameter( "Income1", 0.0f, "A constant applied if a person is from a class 1 income." )]
        public float Income1;

        [RunParameter( "Income2", 0.0f, "A constant applied if a person is from a class 2 or 3 income" )]
        public float Income2;

        [RunParameter( "Income3", 0.0f, "A constant applied if a person is from a class 4 or 5 income." )]
        public float Income3;

        [RunParameter( "Income4", 0.0f, "A constant applied if a person is from a class > 5 income." )]
        public float Income4;

        [RunParameter( "LicenceRequired", true, "Is a license required for this mode?" )]
        public bool LicenceRequired;

        [RunParameter( "LogTime", false, "Should we scale against the log base e of distance?" )]
        public bool LogTime;

        [RunParameter( "ExpTime", false, "Should we scale against the distance^2?" )]
        public bool ExpTime;

        [RunParameter( "Vehicle Name", "AutoType", "The name of the vehicle to use for this mode" )]
        public string VehicleName;

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return CurrentlyFeasible > 0;
        }

        [DoNotAutomate]
        public IVehicleType RequiresVehicle
        {
            get;
            set;
        }

        public bool Feasible(ITrip trip)
        {
            var person = trip.TripChain.Person;
            if ( person.Age < this.MinAge )
            {
                return false;
            }
            if ( this.LicenceRequired && !person.Licence )
            {
                return false;
            }
            var vehicles = person.Household.Vehicles;
            for ( int i = 0; i < vehicles.Length; i++ )
            {
                if ( vehicles[i].VehicleType.VehicleName == this.RequiresVehicle.VehicleName )
                {
                    return true;
                }
            }
            return false;
        }

        public bool Feasible(ITripChain tripChain)
        {
            int vehicleLeftAt = tripChain.Person.Household.HomeZone.ZoneNumber;
            var home = vehicleLeftAt;
            var trips = tripChain.Trips;
            var length = trips.Count;
            for ( int i = 0; i < length; i++ )
            {
                var trip = trips[i];
                var mode = trip.Mode;
                if ( !mode.NonPersonalVehicle )
                {
                    if ( mode.RequiresVehicle.VehicleName == this.RequiresVehicle.VehicleName )
                    {
                        // it is only not feasible if we actually take the mode and we don't have a licence
                        if ( ( trip.OriginalZone.ZoneNumber != vehicleLeftAt ) )
                        {
                            return false;
                        }
                        vehicleLeftAt = trip.DestinationZone.ZoneNumber;
                    }
                }
            }
            return vehicleLeftAt == home;
        }

        public double CalculateV(ITrip trip)
        {
            var person = trip.TripChain.Person;
            var household = person.Household;
            var income = (int)household[this.IncomeName];
            var time = this.AutoData.TravelTime( trip.OriginalZone, trip.DestinationZone, trip.TripStartTime ).ToMinutes();
            // Calculate the time of traveling
            var v = 0f;
            v = this.LinearTravelTimeWeight * time;
            if ( this.LogTime )
            {
                v += this.NonLinearTravelTimeFactor * this.TravelTimeFactor * ( (float)Math.Log( time ) + this.ModeConstant );
            }
            else if ( this.ExpTime )
            {
                v += this.NonLinearTravelTimeFactor * (float)( ( time * time ) + this.ModeConstant );
            }
            if ( household.Vehicles.Length > 1 )
            {
                v += this.ManyCarsOwned;
            }
            if ( person.Licence )
            {
                v += this.DriversLicense;
            }
            if ( person.Youth )
            {
                v += this.Youth;
            }
            else if ( person.YoungAdult )
            {
                v += this.YoungAdult;
            }
            if ( person.Female )
            {
                v += this.Female;
            }
            switch ( income )
            {
                // 1
                case 1:
                    v += this.Income1;
                    break;
                // 2
                case 2:
                case 3:
                    v += this.Income2;
                    break;
                // 3
                case 4:
                case 5:
                    v += this.Income3;
                    break;
                // 4
                case 6:
                case 7:
                case 8:
                default:
                    v += this.Income4;
                    break;
            }
            return v;
        }

        [RunParameter( "VarianceScale", 1.0, "How random the error term of utility will be" )]
        public double VarianceScale
        {
            get;
            set;
        }

        private static Time FifteenMinutes = Time.FromMinutes( 15 );
        /// <summary>
        /// This gets the travel time between zones
        /// </summary>
        /// <param name="origin">Where to start</param>
        /// <param name="destination">Where to go</param>
        /// <param name="time">What time of day is it? (hhmm.ss)</param>
        /// <returns>The amount of time it will take</returns>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return this.AutoData.TravelTime( origin, destination, time );
        }

        public float Cost(IZone origin, IZone destination, Time time)
        {
            return this.AutoData.TravelCost( origin, destination, time );
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            return 0;
        }

        public bool NonPersonalVehicle
        {
            get { return false; }
        }

        [RunParameter( "Network Name", "Auto", "The name of the network that this mode uses." )]
        public string NetworkType
        {
            get;
            set;
        }

        public string Name
        {
            get;
            set;
        }

        public float Progress
        {
            get;
            private set;
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public override string ToString()
        {
            return this.ModeName;
        }

        public bool RuntimeValidation(ref string error)
        {
            if ( String.IsNullOrWhiteSpace( this.ModeName ) )
            {
                error = "All modes require a mode name!";
                return false;
            }
            // Check to see if we actually hav a vehicle name
            if ( String.IsNullOrWhiteSpace( this.VehicleName ) )
            {
                error = "We require a name of the vehicle that is required!";
                return false;
            }
            if ( !FindVehicleType( ref error ) )
            {
                return false;
            }
            return FindAutoData( ref error );
        }

        private bool FindAutoData(ref string error)
        {
            IList<INetworkData> networks;
            networks = this.Root.NetworkData;
            if ( String.IsNullOrWhiteSpace( this.NetworkType ) )
            {
                error = "There was no network type selected for the " + ( String.IsNullOrWhiteSpace( this.ModeName ) ? "Auto" : this.ModeName ) + " mode!";
                return false;
            }
            if ( networks == null )
            {
                error = "There was no Auto Network loaded for the Auto Mode!";
                return false;
            }
            bool found = false;
            foreach ( var network in networks )
            {
                if ( network.NetworkType == this.NetworkType )
                {
                    this.AutoData = network;
                    found = true;
                    break;
                }
            }
            if ( !found )
            {
                error = "We were unable to find the network data with the name \"" + this.NetworkType + "\" in this Model System!";
                return false;
            }
            return true;
        }

        private bool FindVehicleType(ref string error)
        {
            // Find vehicle type
            var vehicleTypes = this.Root.VehicleTypes;
            bool found = false;
            if ( this.Root.AutoType != null && this.Root.AutoType.VehicleName == this.VehicleName )
            {
                this.RequiresVehicle = this.Root.AutoType;
                found = true;
            }
            else if ( vehicleTypes != null )
            {
                foreach ( var type in vehicleTypes )
                {
                    if ( type.VehicleName == this.VehicleName )
                    {
                        this.RequiresVehicle = type;
                        found = true;
                        break;
                    }
                }
            }
            if ( !found )
            {
                error = "We were unable to find the vehicle type called " + this.VehicleName + "!";
                return false;
            }
            return true;
        }
    }
}
