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
using System.IO;
using System.Threading.Tasks;
using TMG.ParameterDatabase;
using XTMF;

namespace TMG.GTAModel.ParameterDatabase
{
    public class AdvancedModeParameterDatabase : IModeParameterDatabase
    {
        [RunParameter( "Mode Choice Database File", "ModeChoiceParameters.csv", "A file containing all of the parameters to be used for each parameter set." )]
        public string DatabaseFile;

        [RunParameter( "Demographic Database File", "ModeChoiceDemographicAlternatives.csv", "A file containing all of the alternative values to be used if a parameter is disabled." )]
        public string DemographicDatabaseFile;

        [RunParameter( "Demographic Switch File", "ModeChoiceDemographicSwitches.csv", "A file containing all of the parameters whether or not to use the original parameter or the disabled parameter." )]
        public string DemographicSwitchFile;

        [SubModelInformation( Description = "Modes", Required = false )]
        public List<IModeParameterAssignment> Modes;

        [RootModule]
        public IModelSystemTemplate Root;

        private bool Blending = false;

        private float CurrentBlendWeight = 0f;

        private List<string[]> DemographicAlternativeParameters = new List<string[]>();

        private List<bool[]> DemographicSwitches = new List<bool[]>();

        private bool Loaded = false;

        private List<string[]> ParameterSets = new List<string[]>();

        public string Name
        {
            get;
            set;
        }

        public int NumberOfParameterSets
        {
            get
            {
                if ( ParameterSets != null )
                {
                    return this.ParameterSets.Count;
                }
                return 0;
            }
        }

        [SubModelInformation( Description = "Parameters", Required = false )]
        public List<Parameter> Parameters { get; private set; }

        public float Progress
        {
            get { return 0; }
        }

        public Tuple<byte, byte, byte> ProgressColour
        {
            get { return null; }
        }

        public void ApplyParameterSet(int parameterSetIndex, int demographicIndex)
        {
            // Check to see if we need to load in our data
            if ( !Loaded )
            {
                Load();
            }
            // Now that we have our data loaded in go and take in our parameters
            SetupParameters( parameterSetIndex, demographicIndex );
            // Check to see if we are doing a blending assignment
            if ( this.Blending )
            {
                AssignBlendedParameters();
            }
            else
            {
                // Now that we have our parameters assign the parameters
                AssignParameters();
            }
        }

        public void CompleteBlend()
        {
            Parallel.For( 0, Modes.Count, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate(int i)
                {
                    this.Modes[i].FinishBlending();
                } );
            this.Blending = false;
        }

        public void InitializeBlend()
        {
            this.Blending = true;
            Parallel.For( 0, Modes.Count, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate(int i)
                {
                    this.Modes[i].StartBlend();
                } );
        }

        public bool RuntimeValidation(ref string error)
        {
            return true;
        }

        public void SetBlendWeight(float currentBlendWeight)
        {
            this.CurrentBlendWeight = currentBlendWeight;
        }

        protected string GetInputFileName(string localPath)
        {
            var fullPath = localPath;
            if ( !Path.IsPathRooted( fullPath ) )
            {
                fullPath = Path.Combine( this.Root.InputBaseDirectory, fullPath );
            }
            return fullPath;
        }

        private void AssignBlendedParameters()
        {
            // now in parallel setup all of our modes at the same time
            if ( this.CurrentBlendWeight == 0 ) return;
            Parallel.For( 0, Modes.Count, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate(int i)
                {
                    this.Modes[i].AssignBlendedParameters( this.Parameters, this.CurrentBlendWeight );
                } );
        }

        private void AssignParameters()
        {
            // now in parallel setup all of our modes at the same time
            Parallel.For( 0, Modes.Count, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
                delegate(int i)
                {
                    this.Modes[i].AssignParameters( this.Parameters );
                } );
        }

        private string[] GetAllButFirst(string[] split)
        {
            string[] temp = new string[split.Length - 1];
            Array.Copy( split, 1, temp, 0, temp.Length );
            return temp;
        }

        private void Load()
        {
            lock ( this )
            {
                System.Threading.Thread.MemoryBarrier();
                if ( this.Loaded ) return;
                // First load in the parameters
                var headers = this.LoadParameters();
                // Next we can load the demographic switches and the alternative values at the same time.
                try
                {
                    Parallel.Invoke(
                        delegate()
                        {
                            this.LoadSwitches( headers );
                        },
                        delegate()
                        {
                            this.LoadAlternatives( headers );
                        } );
                }
                catch ( AggregateException e )
                {
                    if ( e.InnerException is XTMFRuntimeException )
                    {
                        throw new XTMFRuntimeException( e.InnerException.Message );
                    }
                    else
                    {
                        throw new XTMFRuntimeException( e.InnerException.Message + "\r\n" + e.InnerException.StackTrace );
                    }
                }
                // now that we have finished loading, flip that switch
                this.Loaded = true;
                System.Threading.Thread.MemoryBarrier();
            }
        }

        private void LoadAlternatives(string[] headers)
        {
            try
            {
                using ( StreamReader reader = new StreamReader( this.GetInputFileName( this.DemographicDatabaseFile ) ) )
                {
                    string line;
                    // burn header
                    reader.ReadLine();
                    while ( ( line = reader.ReadLine() ) != null )
                    {
                        var split = line.Split( ',' );
                        if ( split.Length < headers.Length + 1 )
                        {
                            continue;
                        }
                        this.DemographicAlternativeParameters.Add( GetAllButFirst( split ) );
                    }
                }
            }
            catch ( IOException )
            {
                throw new XTMFRuntimeException( "We were unable to read the file '" + this.GetInputFileName( this.DemographicDatabaseFile ) + "'. Please make sure this file exists and is not in use." );
            }
        }

        private string[] LoadParameters()
        {
            string[] headers = null;
            try
            {
                using ( StreamReader reader = new StreamReader( this.GetInputFileName( this.DatabaseFile ) ) )
                {
                    string line = reader.ReadLine();
                    headers = ParseHeader( line );
                    SetupParameterObjects( headers );
                    while ( ( line = reader.ReadLine() ) != null )
                    {
                        var split = line.Split( ',' );
                        if ( split.Length < headers.Length + 1 )
                        {
                            continue;
                        }
                        this.ParameterSets.Add( GetAllButFirst( split ) );
                    }
                }
            }
            catch ( IOException )
            {
                throw new XTMFRuntimeException( "We were unable to read the file '" + this.GetInputFileName( this.DatabaseFile ) + "'. Please make sure this file exists and is not in use." );
            }
            return headers;
        }

        private void LoadSwitches(string[] headers)
        {
            try
            {
                using ( StreamReader reader = new StreamReader( this.GetInputFileName( this.DemographicSwitchFile ) ) )
                {
                    int lineNumber = 1;
                    string line;
                    // burn header
                    reader.ReadLine();
                    lineNumber++;
                    while ( ( line = reader.ReadLine() ) != null )
                    {
                        var split = line.Split( ',' );
                        if ( split.Length < headers.Length + 1 )
                        {
                            continue;
                        }
                        bool[] switchLine = new bool[headers.Length];
                        for ( int i = 0; i < switchLine.Length; i++ )
                        {
                            if ( !bool.TryParse( split[i + 1], out switchLine[i] ) )
                            {
                                throw new XTMFRuntimeException( "In the file '" + this.GetInputFileName( DemographicSwitchFile )
                                    + "' on line " + lineNumber + " under column '" + headers[i] + "' we were unable to parse the value '"
                                    + split[i + 1] + "' as a boolean.  Please fix this to be either 'true' or 'false'!" );
                            }
                        }
                        this.DemographicSwitches.Add( switchLine );
                        lineNumber++;
                    }
                }
            }
            catch ( IOException )
            {
                throw new XTMFRuntimeException( "We were unable to read the file '" + this.DemographicSwitchFile + "'. Please make sure this file exists and is not in use." );
            }
        }

        private string[] ParseHeader(string line)
        {
            return GetAllButFirst( line.Split( ',' ) );
        }

        private void SetupParameterObjects(string[] headers)
        {
            var length = headers.Length;
            this.Parameters = new List<Parameter>( length );
            for ( int i = 0; i < length; i++ )
            {
                this.Parameters.Add( new Parameter( headers[i] ) );
            }
        }

        private void SetupParameters(int parameterSetIndex, int demographicIndex)
        {
            var length = Parameters.Count;
            if ( parameterSetIndex < 0 )
            {
                throw new XTMFRuntimeException( "The Mode Choice Parameter Set has to have a non negative index!" );
            }
            if ( demographicIndex < 0 )
            {
                throw new XTMFRuntimeException( "The Mode Choice Demographic Parameter Set has to have a non negative index!" );
            }
            if ( parameterSetIndex >= this.ParameterSets.Count )
            {
                throw new XTMFRuntimeException( "The Mode Choice Parameter Set " + parameterSetIndex + " does not exist, please check!" );
            }
            if ( parameterSetIndex >= this.DemographicAlternativeParameters.Count )
            {
                throw new XTMFRuntimeException( "The Demographic Alternative Parameter Set " + parameterSetIndex + " does not exist, please check!" );
            }
            if ( demographicIndex >= this.DemographicSwitches.Count )
            {
                throw new XTMFRuntimeException( "The Mode Choice Demographic Parameter Set " + demographicIndex + " does not exist, please check!" );
            }
            var parameterSet = this.ParameterSets[parameterSetIndex];
            var demographicAlternative = this.DemographicAlternativeParameters[parameterSetIndex];
            var demographicSwitchLine = this.DemographicSwitches[demographicIndex];
            for ( int i = 0; i < length; i++ )
            {
                // the first part is to check to see which value we should be loading
                if ( demographicSwitchLine[i] )
                {
                    // if it is true, then we use the default value
                    this.Parameters[i].Value = parameterSet[i];
                }
                else
                {
                    // if it is false then we use the alternative value
                    this.Parameters[i].Value = demographicAlternative[i];
                }
            }
        }
    }
}