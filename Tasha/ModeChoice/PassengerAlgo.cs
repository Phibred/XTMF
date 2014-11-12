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
using System.Collections.Generic;
using Tasha.Common;
using XTMF;

namespace Tasha.ModeChoice
{
    public class PassengerAlgo
    {
        private ITashaRuntime TashaRuntime;

        public PassengerAlgo(ITashaRuntime runtime)
        {
            this.TashaRuntime = runtime;
        }

        public void AssignPassengerTrips(ITashaHousehold household)
        {
            //finding all potential trips
            Dictionary<ITashaPerson, List<List<ITripChain>>> PotentialModeChoices = FindAllPotentialModesForTrips( household );
            if ( PotentialModeChoices.Count == 0 ) return;
            //choosing the optimal modes for each person from all the potential trips
            Dictionary<ITashaPerson, List<ITripChain>> OptimalSets = ChooseOptimalSetForEachPerson( PotentialModeChoices );
            if ( OptimalSets != null )
            {
                //removing duplicate passenger trips (ie. Two people carry same passenger)
                RemoveDuplicates( OptimalSets );
                //clearing Auxiliary Trips since they have been used as temporary variables to this point
                ClearPassengerTrips( household );
                //adding auxiliary passenger trips
                AddPassengerTrips( household, OptimalSets );
                //combining connecting Auxiliary trips to tripchains
                FinalizeAuxTrips( household );
            }
        }

        public double CalculateU(ITripChain tripChain)
        {
            ITrip facilitatedTrip = tripChain["FacilitateTrip"] as ITrip;
            ISharedMode facilitatedTripMode = tripChain["SharedMode"] as ISharedMode;
            //the mode data for the facilitated trip
            ModeData facilitatedTripData = ModeData.Get( facilitatedTrip );
            if ( facilitatedTripData == null )
            {
                throw new XTMFRuntimeException( "There was no facilitated Trip Data!" );
            }
            else if ( TashaRuntime == null )
            {
                throw new XTMFRuntimeException( "Tasha runtime was null!" );
            }
            double passengersU = facilitatedTripData.U( facilitatedTripMode.ModeChoiceArrIndex );
            double driversU = CalculateUofAuxTrip( tripChain );
            return passengersU + driversU;
        }

        /// <summary>
        /// Adds the auxiliary trip chain to the person if enough vehicles are available
        /// </summary>
        /// <param name="household"></param>
        /// <param name="OptimalSets"></param>
        private void AddPassengerTrips(ITashaHousehold household, Dictionary<ITashaPerson, List<ITripChain>> OptimalSets)
        {
            foreach ( var optSet in OptimalSets )
            {
                ITashaPerson person = optSet.Key;
                List<ITripChain> optimalSet = optSet.Value;
                if ( person.AuxTripChains == null )
                {
                    person.AuxTripChains = new List<ITripChain>( 5 );
                }
                else
                {
                    person.AuxTripChains.Clear();
                }
                var length = optimalSet.Count;
                for ( int i = 0; i < length; i++ )
                {
                    var vehicles = optimalSet[i].requiresVehicle;
                    var vehiclesLength = vehicles.Count;
                    for ( int j = 0; j < vehiclesLength; j++ )
                    {
                        if ( household.NumberOfVehicleAvailable( new TashaTimeSpan( optimalSet[i].StartTime, optimalSet[i].EndTime ), vehicles[j], true ) > 0 )
                        {
                            person.AuxTripChains.Add( optimalSet[i] );
                        }
                    }
                }
            }
        }

        private double CalculateUofAuxTrip(ITripChain tripChain)
        {
            return (double)tripChain["U"];
        }

        private double CalculateUofTripChainSet(List<ITripChain> tripChains)
        {
            double utility = 0;
            var length = tripChains.Count;
            for ( int i = 0; i < length; i++ )
            {
                utility += CalculateU( tripChains[i] );
            }
            return utility;
        }

        private void ChangeFacilitatedTripMode(ITripChain auxTrip)
        {
            ITrip facilitatedTrip = auxTrip["FacilitateTrip"] as ITrip;
            ISharedMode facilitatedTripMode = auxTrip["FacilitateTripMode"] as ISharedMode;
            facilitatedTrip.Mode = facilitatedTripMode;
            facilitatedTrip.SharedModeDriver = auxTrip.Person;
        }

        private Dictionary<ITashaPerson, List<ITripChain>> ChooseOptimalSetForEachPerson(Dictionary<ITashaPerson, List<List<ITripChain>>> PotentialModeChoices)
        {
            Dictionary<ITashaPerson, List<ITripChain>> optimalTripChainsForPerson = new Dictionary<ITashaPerson, List<ITripChain>>( PotentialModeChoices.Count );

            //For each person find their optimal set of Aux trip chains
            foreach ( var personset in PotentialModeChoices )
            {
                Dictionary<List<ITripChain>, double> tripChainsUtility = new Dictionary<List<ITripChain>, double>();
                var personTripChains = personset.Value;
                var length = personTripChains.Count;
                for ( int i = 0; i < length; i++ )
                {
                    tripChainsUtility.Add( personTripChains[i], CalculateUofTripChainSet( personTripChains[i] ) );
                }

                List<ITripChain> MaxUtilitySet = GetMaxUtilityTripChainSet( tripChainsUtility );

                if ( MaxUtilitySet == null )
                {
                    return null;
                }

                //personset.Key.Attach("OptimalAuxTripChainSet", MaxUtilitySet);

                //adding the max utility set
                optimalTripChainsForPerson.Add( personset.Key, MaxUtilitySet );
            }

            return optimalTripChainsForPerson;
        }

        private void ClearPassengerTrips(ITashaHousehold household)
        {
            var persons = household.Persons;
            var personsLength = persons.Length;
            for ( int i = 0; i < personsLength; i++ )
            {
                var auxTC = persons[i].AuxTripChains;
                if ( auxTC == null )
                {
                    persons[i].AuxTripChains = new List<ITripChain>( 5 );
                }
                else
                {
                    persons[i].AuxTripChains.Clear();
                }
            }
        }

        /// <summary>
        /// Copies a trip chain to a new trip chain
        /// </summary>
        /// <param name="tripchains">The trip chain to copy</param>
        /// <param name="newTripChain">The new trip chain</param>
        private void CopyChain(List<ITripChain> tripchains, out List<ITripChain> newTripChain)
        {
            var length = tripchains.Count;
            newTripChain = new List<ITripChain>( length );
            for ( int i = 0; i < length; i++ )
            {
                newTripChain.Add( tripchains[i] );
            }
        }

        private void FinalizeAuxTrips(ITashaHousehold household)
        {
            var persons = household.Persons;
            var pLength = persons.Length;
            for ( int i = 0; i < pLength; i++ )
            {
                var aux = persons[i].AuxTripChains;
                var auxLength = aux.Count;
                for ( int j = 0; j < auxLength; j++ )
                {
                    ITrip connectingTripChain = aux[j]["ConnectingChain"] as ITrip;
                    Activity purpose = (Activity)aux[j]["Purpose"];
                    ITrip facilitatedTrip = aux[j]["FacilitateTrip"] as ITrip;
                    ISharedMode facilitatedTripMode = aux[j]["SharedMode"] as ISharedMode;
                    facilitatedTrip.Mode = facilitatedTripMode;
                    var trips = aux[j].Trips;
                    var tripsLength = trips.Count;
                    var associatedMode = facilitatedTripMode.AssociatedMode;
                    for ( int t = 0; t < tripsLength; t++ )
                    {
                        trips[t].Mode = associatedMode;
                    }
                }
            }
        }

        private Dictionary<ITashaPerson, List<List<ITripChain>>> FindAllPotentialModesForTrips(ITashaHousehold household)
        {
            Dictionary<ITashaPerson, List<List<ITripChain>>> possibleChains = new Dictionary<ITashaPerson, List<List<ITripChain>>>( household.Persons.Length);
            var people = household.Persons;
            var peopleLength = people.Length;
            for ( int i = 0; i < peopleLength; i++ )
            {
                // check to see if there are no aux chain, if so just continue on
                if ( people[i].AuxTripChains.Count == 0 )
                {
                    continue;
                }
                List<List<ITripChain>> potentialChains = new List<List<ITripChain>>();
                //sorting trips by start time (prereq for getting conflicting chains)
                sortTrips( people[i].AuxTripChains );
                findPotentialTripChainsRec( people[i].AuxTripChains, 0, potentialChains, 0 );
                possibleChains.Add( people[i], potentialChains );
            }
            return possibleChains;
        }

        private ITripChain FindHighestUtility(Dictionary<ITripChain, double> conflictingUtilities)
        {
            double max = double.MinValue;
            ITripChain best = null;
            foreach ( var element in conflictingUtilities )
            {
                if ( element.Value > max )
                {
                    max = element.Value;
                    best = element.Key;
                }
            }
            return best;
        }

        /// <summary>
        /// Recursive algorithm to find all potential non conflicting chains from the given set
        /// </summary>
        /// <param name="tripchains"></param>
        /// <param name="currentChain"></param>
        /// <param name="potentialChains"></param>
        /// <param name="utility"></param>
        private void findPotentialTripChainsRec(List<ITripChain> tripchains,
            int currentChain, List<List<ITripChain>> potentialChains, double utility)
        {
            if ( currentChain >= tripchains.Count )
            {
                potentialChains.Add( tripchains );
                return;
            }

            ITripChain currentTripChain = tripchains[currentChain];
            //gets all trip chains occuring at the same time as this one
            List<ITripChain> conflictingChains = getConflictingChains( tripchains[currentChain], tripchains );
            //no conflicting chains so include it
            if ( conflictingChains.Count == 0 )
            {
                //no conflicting set so continue
                findPotentialTripChainsRec( tripchains, ++currentChain, potentialChains, utility );
            }
            else
            {
                //copy this trip chain to a new one
                List<ITripChain> tripChainRemovedConflicts;
                CopyChain( tripchains, out tripChainRemovedConflicts );
                var length = conflictingChains.Count;
                for ( int i = 0; i < length; i++ )
                {
                    tripChainRemovedConflicts.Remove( conflictingChains[i] );
                }
                //Find potential tripchains with this trip chain included
                findPotentialTripChainsRec( tripChainRemovedConflicts, currentChain + 1, potentialChains, utility );
                List<ITripChain> tripChainWithoutThisChain;
                CopyChain( tripchains, out tripChainWithoutThisChain );
                tripChainWithoutThisChain.Remove( currentTripChain );
                //Find potential tripchain without this trip chain included
                findPotentialTripChainsRec( tripChainWithoutThisChain, currentChain, potentialChains, utility );
            }
        }

        private Time getAuxTripChainEndTime(ITripChain auxTripChain)
        {
            if ( auxTripChain["ConnectingChain"] == null )
            {
                return auxTripChain.EndTime;
            }
            else
            {
                ITrip connectingChain1 = auxTripChain["ConnectingChain"] as ITrip;
                Activity purpose = (Activity)auxTripChain["Purpose"];
                Time endTime;
                if ( purpose == Activity.Dropoff )
                {
                    endTime = connectingChain1.TripChain.EndTime;
                }
                else
                {
                    endTime = auxTripChain.EndTime;
                }
                return endTime;
            }
        }

        private Time getAuxTripChainStartTime(ITripChain auxTripChain)
        {
            if ( auxTripChain["ConnectingChain"] == null )
            {
                return auxTripChain.StartTime;
            }
            else
            {
                ITrip connectingChain1 = auxTripChain["ConnectingChain"] as ITrip;
                Activity purpose = (Activity)auxTripChain["Purpose"];
                Time startTime;
                if ( purpose != Activity.Dropoff )
                {
                    startTime = connectingChain1.TripChain.StartTime;
                }
                else
                {
                    startTime = auxTripChain.StartTime;
                }
                return startTime;
            }
        }

        /// <summary>
        /// Gets all the conflicting trip chains to a given trip chain
        /// </summary>
        /// <param name="tripchain">the given trip chain</param>
        /// <param name="tripchains">the trip chains to compare with</param>
        /// <returns></returns>
        private List<ITripChain> getConflictingChains(ITripChain tripchain, List<ITripChain> tripchains)
        {
            List<ITripChain> conflictingChains = new List<ITripChain>();
            ITashaPerson driver = tripchain.Person;
            Time startTime = getAuxTripChainStartTime( tripchain );
            Time endTime = getAuxTripChainEndTime( tripchain );
            var length = tripchains.Count;
            for ( int i = 0; i < length; i++ )
            {
                if ( tripchains[i] == tripchain )
                {
                    continue;
                }
                //passed the trip chain no need to keep checking
                if ( tripchains[i].StartTime > tripchain.EndTime )
                {
                    return conflictingChains;
                }
                Time otherStartTime = getAuxTripChainStartTime( tripchains[i] );
                Time otherEndTime = getAuxTripChainEndTime( tripchains[i] );
                if ( otherStartTime < endTime && otherEndTime > startTime )
                {
                    conflictingChains.Add( tripchains[i] );
                }
            }
            return conflictingChains;
        }

        private List<ITripChain> GetMaxUtilityTripChainSet(Dictionary<List<ITripChain>, double> tripChainsUtility)
        {
            double max = double.MinValue;
            List<ITripChain> maxSet = null;
            foreach ( var element in tripChainsUtility )
            {
                if ( element.Value > max )
                {
                    max = element.Value;
                    maxSet = element.Key;
                }
            }
            return maxSet;
        }

        /// <summary>
        /// Removes Duplicate Passenger trips (ie. two ppl facilitating one passenger
        /// </summary>
        /// <param name="OptimalSets"></param>
        /// <returns></returns>
        private void RemoveDuplicates(Dictionary<ITashaPerson, List<ITripChain>> OptimalSets)
        {
            List<ITripChain> duplicates = new List<ITripChain>();
            //finding duplicates
            foreach ( var s in OptimalSets )
            {
                ITashaPerson person = s.Key;
                List<ITripChain> tripChains = s.Value;
                var tripChainsLength = tripChains.Count;
                for ( int i = 0; i < tripChainsLength; i++ )
                {
                    Dictionary<ITripChain, double> conflictingUtilities = new
                                                          Dictionary<ITripChain, double>();
                    //the faciliated trip
                    ITrip facilitatedTrip = tripChains[i]["FacilitateTrip"] as ITrip;
                    double uOfAuxTrip = this.CalculateUofAuxTrip( tripChains[i] );
                    conflictingUtilities.Add( tripChains[i], uOfAuxTrip );
                    //finding tripchain and U of duplicate passenger trips
                    foreach ( var s2 in OptimalSets )
                    {
                        if ( s.Key == s2.Key )
                        {
                            continue;
                        }
                        ITashaPerson person2 = s2.Key;
                        List<ITripChain> tripChains2 = s2.Value;
                        var tripChain2Length = tripChains2.Count;
                        for ( int j = 0; j < tripChain2Length; j++ )
                        {
                            ITrip facilitatedTrip2 = tripChains2[j]["FacilitateTrip"] as ITrip;

                            if ( facilitatedTrip2 == facilitatedTrip )
                            {
                                conflictingUtilities.Add( tripChains2[j], this.CalculateUofAuxTrip( tripChains2[j] ) );
                            }
                        }
                    }
                    ITripChain best = FindHighestUtility( conflictingUtilities );
                    conflictingUtilities.Remove( best );
                    foreach ( var element in conflictingUtilities )
                    {
                        duplicates.Add( element.Key );
                    }
                }
            }
            //removing duplicates
            foreach ( var s in OptimalSets )
            {
                ITashaPerson person = s.Key;
                List<ITripChain> tripChains = s.Value;
                var duplicatesLength = duplicates.Count;
                for ( int i = 0; i < duplicatesLength; i++ )
                {
                    // Remove the duplicate if it exists
                    tripChains.Remove( duplicates[i] );
                }
            }
        }

        /// <summary>
        /// Sorts the trip chains in the list by start time
        /// </summary>
        /// <param name="list"></param>
        private void sortTrips(List<ITripChain> list)
        {
            int length = list.Count;
            for ( int i = 0; i < length; i++ )
            {
                int min = i;
                for ( int j = i + 1; j < length; j++ )
                {
                    if ( list[min].StartTime > list[j].StartTime )
                    {
                        min = j;
                    }
                }
                if ( min != i )
                {
                    var temp = list[min];
                    list[min] = list[i];
                    list[i] = temp;
                }
            }
        }
    }
}