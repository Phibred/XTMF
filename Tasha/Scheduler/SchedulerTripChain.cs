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
using System.Collections.Concurrent;
using System.Collections.Generic;
using Tasha.Common;
using XTMF;

namespace Tasha.Scheduler
{
    internal sealed class SchedulerTripChain : Attachable, ITripChain
    {
        private static ConcurrentQueue<SchedulerTripChain> Chains = new ConcurrentQueue<SchedulerTripChain>();

        private SchedulerTripChain(ITashaPerson person)
        {
            this.Person = person;
            this.Trips = new List<ITrip>( 3 );
        }

        /// <summary>
        /// The End Time of this Trip Chain (The time returned home)
        /// </summary>
        public Time EndTime
        {
            get
            {
                return this.Trips[this.Trips.Count - 1].ActivityStartTime;
            }
        }

        public ITripChain GetRepTripChain
        {
            get;
            set;
        }

        /// <summary>
        /// Is this a joint trip?
        /// </summary>
        public bool JointTrip
        {
            get
            {
                return JointTripID != 0;
            }
        }

        public List<ITripChain> JointTripChains
        {
            get
            {
                if ( !this.JointTrip ) return null;

                List<ITripChain> linkedTripChains = new List<ITripChain>();
                foreach ( var p in this.Person.Household.Persons )
                {
                    foreach ( var tripChain in p.TripChains )
                    {
                        if ( tripChain.JointTripID == this.JointTripID )
                            linkedTripChains.Add( tripChain );
                    }
                }
                return linkedTripChains;
            }
        }

        /// <summary>
        /// What is the ID of this joint trip?
        /// </summary>
        public int JointTripID
        {
            get;
            internal set;
        }

        /// <summary>
        /// Is the owned the Representative for the joint trip?
        /// </summary>
        public bool JointTripRep
        {
            get;
            internal set;
        }

        public List<ITashaPerson> passengers
        {
            get { return null; }
        }

        /// <summary>
        /// The person that this trip chain belongs to
        /// </summary>
        public ITashaPerson Person
        {
            get;
            set;
        }

        public List<IVehicleType> requiresVehicle
        {
            get
            {
                List<IVehicleType> v = new List<IVehicleType>();

                foreach ( var trip in Trips )
                {
                    if ( trip.Mode != null && trip.Mode.RequiresVehicle != null )
                    {
                        if ( !v.Contains( trip.Mode.RequiresVehicle ) )
                        {
                            v.Add( trip.Mode.RequiresVehicle );
                        }
                    }
                }

                return v;
            }
        }

        /// <summary>
        /// The Start Time of this TripChain
        /// </summary>
        public Time StartTime
        {
            get
            {
                return this.Trips[0].TripStartTime;
            }
        }

        public bool TripChainRequiresPV
        {
            get
            {
                foreach ( var t in this.Trips )
                {
                    if ( !t.Mode.NonPersonalVehicle )
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// The trips in this trip chain
        /// </summary>
        public List<Tasha.Common.ITrip> Trips
        {
            get;
            set;
        }

        /// <summary>
        /// Shallow clone of this trip chain (does not clone trips)
        /// </summary>
        /// <returns></returns>
        public ITripChain Clone()
        {
            ITripChain chain = (ITripChain)this.MemberwiseClone();
            chain.Trips = new List<ITrip>();
            chain.Trips.AddRange( this.Trips );
            return chain;
        }

        /// <summary>
        /// Clones this trip chain and its trips
        /// </summary>
        /// <returns></returns>
        public ITripChain DeepClone()
        {
            //shallow clone of tripchain
            ITripChain chain = (ITripChain)this.MemberwiseClone();
            chain.Trips = new List<ITrip>();
            List<ITrip> trips = new List<ITrip>();

            //cloning trips as well and setting their trip chain to cloned chained
            foreach ( var trip in this.Trips )
            {
                ITrip t = trip.Clone();
                t.TripChain = chain;
                trips.Add( t );
            }

            chain.Trips.AddRange( trips );

            return chain;
        }

        public void Recycle()
        {
            this.Release();
            foreach ( var t in this.Trips )
            {
                t.Release();
            }
            this.Trips.Clear();
            this.JointTripID = 0;
            this.JointTripRep = false;
            this.GetRepTripChain = null;
            if ( this.JointTripChains != null )
            {
                this.JointTripChains.Clear();
            }
            this.Person = null;
            Chains.Enqueue( this );
        }

        internal static SchedulerTripChain GetTripChain(ITashaPerson person)
        {
            SchedulerTripChain ret;
            if ( !Chains.TryDequeue( out ret ) )
            {
                return new SchedulerTripChain( person );
            }
            ret.Person = person;
            return ret;
        }
    }
}