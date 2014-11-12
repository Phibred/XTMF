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
using Tasha.Common;
using TMG;
using XTMF;

namespace Tasha.Modes
{
    /// <summary>
    /// Mode RideShare that respresents pure joint trips
    /// </summary>
    public sealed class RideShare : ISharedMode
    {
        [RunParameter( "AllowNonMemberDriver", true, "Allow non-members to drive" )]
        public bool AllowNonMemberDriver;

        [RunParameter( "cridesh", 0f, "The constant factor for the Ride Share mode" )]
        public float cridesh;

        [RunParameter( "dpurp_oth_drive", 0f, "The constant factory applied if the purpose of the trip is 'Other'" )]
        public float dpurp_oth_drive;

        [RunParameter( "dpurp_shop_drive", 0f, "The constant factory applied if the purpose of the trip is 'Shopping'" )]
        public float dpurp_shop_drive;

        [RunParameter( "parking", 0f, "The factor applied to the cost of parking" )]
        public float parking;

        [RootModule]
        public ITashaRuntime TashaRuntime;

        [RunParameter( "travelCost", 0f, "The factor applied to the travel cost" )]
        public float travelCost;

        [RunParameter( "travelTime", 0f, "The factor applied the the travel time" )]
        public float travelTime;

        private byte modeChoiceArrIndex = 0;

        /// <summary>
        /// Create a new Auto mode, this will be called when
        /// Tasha# loads us
        /// </summary>
        public RideShare()
        {
        }

        [DoNotAutomate]
        public ITashaMode AssociatedMode
        {
            get { return this.TashaRuntime.AutoMode; }
        }

        [Parameter( "Demographic Category Feasible", 1f, "(Automated by IModeParameterDatabase)\r\nIs the currently processing demographic category feasible?" )]
        public float CurrentlyFeasible { get; set; }

        /// <summary>
        /// Index in the Mode Array (in the List of possible Modes)
        /// </summary>
        public byte ModeChoiceArrIndex
        {
            get
            {
                return modeChoiceArrIndex;
            }

            set
            {
                this.modeChoiceArrIndex = value;
            }
        }

        [RunParameter( "Name", "RideShare", "The name of the mode" )]
        public string ModeName { get; set; }

        /// <summary>
        ///
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        public string NetworkType
        {
            get;
            set;
        }

        /// <summary>
        /// This does not require a personal vehicle
        /// </summary>
        public bool NonPersonalVehicle
        {
            get { return false; }
        }

        [RunParameter( "Observed Mode Character Code", 'A', "The character code used for model estimation." )]
        public char ObservedMode
        {
            get;
            set;
        }

        /// <summary>
        /// Output signature for Passenger Type
        /// </summary>
        [RunParameter( "Observed Signature Code", 'A', "The character code used for model output." )]
        public char OutputSignature
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
            get { return new Tuple<byte, byte, byte>( 100, 200, 100 ); }
        }

        [DoNotAutomate]
        /// <summary>
        /// This does not require a personal vehicle
        /// </summary>
        public IVehicleType RequiresVehicle
        {
            get { return this.TashaRuntime.AutoType; }
        }

        [DoNotAutomate]
        public INetworkData TravelData { get; set; }

        [RunParameter( "Variance Scale", 1.0f, "The scale for varriance used for variance testing." )]
        public double VarianceScale
        {
            get;
            set;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="trip"></param>
        /// <returns></returns>
        public double CalculateV(ITrip trip)
        {
            double V = 0;

            V += this.cridesh;

            //calculate the transit time
            IZone o = trip.OriginalZone, d = trip.DestinationZone;
            V += TravelTime( o, d, trip.ActivityStartTime ).ToFloat() * this.travelTime;
            V += Cost( o, d, trip.TripStartTime ) * this.travelCost;
            V += d.ParkingCost * parking;

            if ( trip.Purpose == Activity.Market | trip.Purpose == Activity.JointMarket ) V += this.dpurp_shop_drive;
            if ( trip.Purpose == Activity.IndividualOther | trip.Purpose == Activity.JointOther ) V += this.dpurp_oth_drive;

            return V;
        }

        public float CalculateV(IZone origin, IZone destination, Time time)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Basic auto gas price from origin zone to destination zone
        /// </summary>
        /// <param name="origin"></param>
        /// <param name="destination"></param>
        /// <returns></returns>
        public float Cost(IZone origin, IZone destination, Time time)
        {
            return this.TravelData.TravelCost( origin, destination, time );
        }

        public bool Feasible(IZone origin, IZone destination, Time timeOfDay)
        {
            return CurrentlyFeasible > 0;
        }

        /// <summary>
        /// Determines if assigning a ride share mode to this trip chain is feasible.
        /// Criteria: Someone must be assigned a car on this trip chain (not necessarily the rep).
        /// </summary>
        /// <param name="tripChain"></param>
        /// <returns></returns>
        public bool Feasible(ITripChain tripChain)
        {
            if ( tripChain.Person.Household.Vehicles == null ) return false;
            return tripChain.Person.Household.Vehicles.Length > 0;
        }

        /// <summary>
        /// Checking if the trip is feasible -- ie someone is able to drive
        /// </summary>
        /// <param name="trip">the trip to test feasibility on</param>
        /// <returns>Is this trip feasible?</returns>
        public bool Feasible(ITrip trip)
        {
            //only handles joint tours.... so dont handle single rides
            if ( !trip.TripChain.JointTrip ) return false;

            if ( trip.TripChain.Person.Household.Vehicles == null ) return false;
            return trip.TripChain.Person.Household.Vehicles.Length > 0;
        }

        /// <summary>
        ///
        /// </summary>
        public bool IsObservedMode(char observedMode)
        {
            return ( observedMode == this.ObservedMode );
        }

        /// <summary>
        ///
        /// </summary>
        public void ModeChoiceIterationComplete()
        {
            //do nothing here
        }

        /// <summary>
        /// This is called before the start method as a way to pre-check that all of the parameters that are selected
        /// are in fact valid for this module.
        /// </summary>
        /// <param name="error">A string that should be assigned a detailed error</param>
        /// <returns>If the validation was successful or if there was a problem</returns>
        public bool RuntimeValidation(ref string error)
        {
            var networks = this.TashaRuntime.NetworkData;
            if ( networks == null )
            {
                error = "There was no Auto Network loaded for the Rideshare Mode!";
                return false;
            }
            bool found = false;
            foreach ( var network in networks )
            {
                if ( network.NetworkType == "Auto" )
                {
                    this.TravelData = network;
                    found = true;
                    break;
                }
            }
            if ( !found )
            {
                error = "We were unable to find the network data with the name \"Auto\" in this Model System!";
                return false;
            }
            return true;
        }

        /// <summary>
        /// This gets the travel time between zones
        /// </summary>
        /// <param name="origin">Where to start</param>
        /// <param name="destination">Where to go</param>
        /// <param name="time">What time of day is it? (hhmm.ss)</param>
        /// <returns>The amount of time it will take</returns>
        public Time TravelTime(IZone origin, IZone destination, Time time)
        {
            return this.TravelData.TravelTime( origin, destination, time );
        }
    }
}